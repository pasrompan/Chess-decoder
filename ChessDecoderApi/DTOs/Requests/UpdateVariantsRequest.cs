namespace ChessDecoderApi.DTOs.Requests;

/// <summary>
/// Request model for persisting the variant tree captured in Explore mode.
/// </summary>
/// <remarks>
/// <para>
/// The payload is a serialized JSON blob mirroring the frontend
/// <c>GameVariants</c> shape (<c>{ nodes, rootsByPly }</c>). The backend
/// stores it opaquely so the schema can evolve without database migrations.
/// </para>
/// <para>
/// <see cref="VariantsJson"/> may be <c>null</c> or empty to clear all
/// variants for the game.
/// </para>
/// </remarks>
public class UpdateVariantsRequest
{
    public string? VariantsJson { get; set; }
}
