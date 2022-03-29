using System.Text.Json.Serialization;

namespace Inviter.Server;

public class Error
{
    [JsonPropertyName("error")]
    public string ErrorType { get; }

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; }

    public Error(string errorType)
    {
        ErrorType = errorType;
        ErrorMessage = "An unknown error has occurerd.";
    }

    public Error(string errorType, string errorMessage)
    {
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }
}