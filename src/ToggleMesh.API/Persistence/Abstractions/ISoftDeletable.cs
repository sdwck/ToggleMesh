namespace ToggleMesh.API.Persistence.Abstractions;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
