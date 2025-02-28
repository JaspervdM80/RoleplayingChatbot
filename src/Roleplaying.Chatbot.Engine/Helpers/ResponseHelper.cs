namespace Roleplaying.Chatbot.Engine.Helpers;

public static class ResponseHelper
{
    public static string CleanJsonResponse(string response)
    {
        response = response.Trim();

        // Remove markdown code block formatting if present
        if (response.StartsWith("```json") || response.StartsWith("```"))
        {
            response = response.Replace("```json", "").Replace("```", "").Trim();
        }

        return response.Trim();
    }
}
