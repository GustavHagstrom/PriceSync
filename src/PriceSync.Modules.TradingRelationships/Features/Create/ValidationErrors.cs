namespace PriceSync.Modules.TradingRelationships.Features.Create;

internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool HasErrors => _errors.Count > 0;

    public void Add(string key, string error)
    {
        if (!_errors.TryGetValue(key, out var errors))
        {
            errors = [];
            _errors[key] = errors;
        }

        errors.Add(error);
    }

    public IReadOnlyDictionary<string, string[]> GetErrors()
    {
        return _errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }
}
