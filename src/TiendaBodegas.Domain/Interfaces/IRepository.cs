namespace TiendaBodegas.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    T? GetById(int id);
    IEnumerable<T> GetAll();
    void Add(T entity);
}