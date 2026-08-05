namespace ETL.Core.Extract;

public interface IExtractor<T>
{
    Task<List<T>> ExtractAsync(CancellationToken cancellationToken = default);
}