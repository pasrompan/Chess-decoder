namespace ChessDecoderApi.Exceptions;

public class UserNotFoundException : Exception
{
    public string UserId { get; }

    public UserNotFoundException(string userId) : base($"User with ID '{userId}' not found")
    {
        UserId = userId;
    }

    public UserNotFoundException(string userId, Exception innerException) : base($"User with ID '{userId}' not found", innerException)
    {
        UserId = userId;
    }
}
