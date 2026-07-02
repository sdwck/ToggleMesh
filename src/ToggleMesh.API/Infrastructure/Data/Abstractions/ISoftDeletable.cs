namespace ToggleMesh.API.Infrastructure.Data.Abstractions;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
