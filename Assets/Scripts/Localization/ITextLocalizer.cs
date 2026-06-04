public interface ITextLocalizer
{
    string Locale { get; }
    bool Load(string locale);
    string Get(string key);
}
