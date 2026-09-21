namespace PriceSync.BuildingBlocks.Validation;

public class ValidationErrors
{
    private Dictionary<string, List<string>> Errors { get; } = new Dictionary<string, List<string>>();
    public IReadOnlyDictionary<string, string[]> GetErrors() => Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    public void Add(string key, string error)
    {
        if (!Errors.ContainsKey(key))
        {
            Errors[key] = new List<string>();
        }
        Errors[key].Add(error);
    }

    public bool HasErrors => Errors.Count > 0;
}
