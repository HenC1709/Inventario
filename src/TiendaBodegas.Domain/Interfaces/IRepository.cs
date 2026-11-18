namespace TiendaBodegas.Domain.Interfaces;

/// <summary>
/// Generic repository contract defining basic CRUD operations for domain entities.
/// </summary>
/// <typeparam name="T">The entity type managed by the repository.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    T? GetById(int id);

    /// <summary>
    /// Retrieves all entities in the repository.
    /// </summary>
    IEnumerable<T> GetAll();

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    void Add(T entity);
}