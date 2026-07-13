namespace ToggleMesh.CLI.Generators;

public interface ICodeGenerator
{
    string LanguageName { get; }
    string DefaultFileName { get; }
    string Generate(Dictionary<string, string> mappedKeys);
}
