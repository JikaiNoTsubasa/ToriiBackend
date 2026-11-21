using Microsoft.EntityFrameworkCore;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>
/// Files of ticket channels: stored once on disk (<see cref="StoredFile"/>), attached to
/// channels through <see cref="ChannelFile"/> links. Uploading and forwarding are recorded in
/// the channel's activity feed. Access always goes through a channel the user sees.
/// </summary>
public partial class TicketManager
{
    /// <summary>A file content opened for download.</summary>
    public sealed record FileContent(Stream Content, string FileName, string ContentType);

    /// <summary>
    /// Uploads files to a channel the user sees (FILE_UPLOADED activity for each). Files are
    /// written to disk first; if saving the metadata fails, the written files are removed.
    /// </summary>
    public async Task<IReadOnlyList<FileRefDto>> UploadFilesAsync(
        string number, long channelId, IReadOnlyList<IFormFile> files, long userId, CancellationToken ct = default)
    {
        if (files.Count == 0)
            throw new ValidationException("Select at least one file to upload.");

        var maxBytes = storageOptions.Value.MaxFileSizeBytes;
        var tooLarge = files.Where(f => f.Length > maxBytes).Select(f => f.FileName).ToList();
        if (tooLarge.Count > 0)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["files"] = [$"Files must not exceed {storageOptions.Value.MaxFileSizeMb} MB: {string.Join(", ", tooLarge)}."],
            });
        if (files.Any(f => f.Length == 0))
            throw new ValidationException("Empty files cannot be uploaded.");

        var channel = await FindVisibleChannelAsync(number, channelId, userId, ct);

        var written = new List<string>();
        try
        {
            var now = DateTime.UtcNow;
            foreach (var file in files)
            {
                await using var content = file.OpenReadStream();
                var key = await storage.SaveAsync(content, ct);
                written.Add(key);

                var link = new ChannelFile
                {
                    TicketChannel = channel,
                    StoredFile = new StoredFile
                    {
                        StorageKey = key,
                        FileName = SanitizeFileName(file.FileName),
                        ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                        Size = file.Length,
                        CreatedAt = now,
                        CreatedById = userId,
                        UpdatedAt = null,
                    },
                    CreatedAt = now,
                    CreatedById = userId,
                    UpdatedAt = null,
                };
                _dbContext.ChannelFiles.Add(link);
                _dbContext.TicketActivities.Add(new TicketActivity
                {
                    TicketChannel = channel,
                    Type = TicketActivityType.FILE_UPLOADED,
                    ActorId = userId,
                    CreatedAt = now,
                    ChannelFile = link,
                });
            }

            channel.UpdatedAt = now;
            channel.UpdatedById = userId;
            await _dbContext.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var key in written)
                storage.Delete(key);
            throw;
        }

        return await _dbContext.StoredFiles
            .AsNoTracking()
            .Where(f => written.Contains(f.StorageKey))
            .OrderBy(f => f.Id)
            .Select(f => new FileRefDto(f.Id, f.FileName, f.ContentType, f.Size))
            .ToListAsync(ct);
    }

    /// <summary>Opens a file attached to a channel the user sees (404 otherwise).</summary>
    public async Task<FileContent> OpenFileAsync(
        string number, long channelId, long fileId, long userId, CancellationToken ct = default)
    {
        var file = await FindVisibleFileAsync(number, channelId, fileId, userId, ct);
        return new FileContent(storage.OpenRead(file.StorageKey), file.FileName, file.ContentType);
    }

    /// <summary>
    /// Forwards a file of a channel the user sees to another channel of the ticket, by linking
    /// the same stored file (no copy): an existing channel of the user, or the channel between
    /// one of their customers and a target customer (reused, or created with an active edge).
    /// 409 when the file is already in the target channel.
    /// </summary>
    public async Task<ForwardFileResultDto> ForwardFileAsync(
        string number, long channelId, long fileId, ForwardFileRequest request, long userId, CancellationToken ct = default)
    {
        var hasChannel = request.TargetChannelId is not null;
        var hasPair = request.SourceCustomerId is not null && request.TargetCustomerId is not null;
        if (hasChannel == hasPair)
            throw new ValidationException("Choose either an existing channel or a source and a target customer.");

        await FindVisibleFileAsync(number, channelId, fileId, userId, ct);

        var (target, created) = hasChannel
            ? (await FindVisibleChannelAsync(number, request.TargetChannelId!.Value, userId, ct), false)
            : await FindOrCreateChannelAsync(number, request.SourceCustomerId!.Value, request.TargetCustomerId!.Value, userId, ct);

        if (!created && target.Id == channelId)
            throw new ValidationException("The file is already in this channel: pick another one.");

        if (!created && await _dbContext.ChannelFiles.AnyAsync(cf =>
                cf.TicketChannelId == target.Id && cf.StoredFileId == fileId && !cf.IsDeleted, ct))
            throw new ConflictException("This file is already attached to the target channel.");

        var now = DateTime.UtcNow;
        var link = new ChannelFile
        {
            TicketChannel = target,
            StoredFileId = fileId,
            ForwardedFromChannelId = channelId,
            CreatedAt = now,
            CreatedById = userId,
            UpdatedAt = null,
        };
        _dbContext.ChannelFiles.Add(link);
        _dbContext.TicketActivities.Add(new TicketActivity
        {
            TicketChannel = target,
            Type = TicketActivityType.FILE_FORWARDED,
            ActorId = userId,
            CreatedAt = now,
            ChannelFile = link,
        });
        if (!created)
        {
            target.UpdatedAt = now;
            target.UpdatedById = userId;
        }
        await _dbContext.SaveChangesAsync(ct);

        return new ForwardFileResultDto(target.Id, created);
    }

    /// <summary>The stored file, when it is attached to a channel of the ticket that the user sees.</summary>
    private async Task<StoredFile> FindVisibleFileAsync(
        string number, long channelId, long fileId, long userId, CancellationToken ct)
    {
        await FindVisibleChannelAsync(number, channelId, userId, ct);
        return await _dbContext.ChannelFiles
                   .AsNoTracking()
                   .Where(cf => cf.TicketChannelId == channelId && cf.StoredFileId == fileId && !cf.IsDeleted)
                   .Select(cf => cf.StoredFile)
                   .FirstOrDefaultAsync(ct)
               ?? throw NotFoundException.For("File", fileId);
    }

    /// <summary>File name without any path, control characters, or excessive length.</summary>
    private static string SanitizeFileName(string name)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));
        var cleaned = new string(fileName.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0)
            cleaned = "file";
        return cleaned.Length <= 255 ? cleaned : cleaned[^255..];
    }
}
