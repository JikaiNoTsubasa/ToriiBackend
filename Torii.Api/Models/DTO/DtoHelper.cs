namespace Torii.Api.Models.DTO;

public static class DtoHelper
{
    public static TDto ToDto<TEntity, TDto>(this TEntity entity)
        where TEntity : Entity
        where TDto : class, new()
    {
        var dto = new TDto();
        var entityProperties = typeof(TEntity).GetProperties();
        var dtoProperties = typeof(TDto).GetProperties();

        foreach (var entityProperty in entityProperties)
        {
            var dtoProperty = dtoProperties.FirstOrDefault(p => p.Name == entityProperty.Name);
            if (dtoProperty != null && dtoProperty.CanWrite)
            {
                var value = entityProperty.GetValue(entity);
                dtoProperty.SetValue(dto, value);
            }
        }

        return dto;
    }
}
