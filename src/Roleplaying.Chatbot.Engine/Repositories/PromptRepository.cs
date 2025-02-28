namespace Roleplaying.Chatbot.Engine.Repositories;

public class PromptRepository
{
    private readonly Dictionary<string, string> _prompts;

    public PromptRepository()
    {
        _prompts = [];
    }

    public async Task LoadAsync()
    {
        var directory = new DirectoryInfo("./Prompts");

        foreach (var file in directory.GetFiles("*.txt"))
        {
            var filename = Path.GetFileNameWithoutExtension(file.Name);
            var prompt = await File.ReadAllTextAsync(file.FullName);

            Add(filename, prompt);
        }
    }

    public void Add(string key, string prompt)
    {
        _prompts.Add(key, prompt);
    }

    public string Get(string key)
    {
        return _prompts.TryGetValue(key, out var prompt) ? prompt : string.Empty;
    }
}

