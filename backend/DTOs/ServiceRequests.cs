namespace Trippy.Backend.DTOs;

// Service-layer input records for the itinerary CRUD services. Kept separate from the
// controller-facing DTOs above (PlaceRead, ItineraryCreate, ...) so the OpenAPI/Orval
// contract only ever reflects the DTOs, never these internal shapes.

public sealed record CreateItineraryRequest(string Name, string? Description, DateOnly StartDate);

public sealed record UpdateItineraryRequest(string? Name, string? Description, DateOnly? StartDate);

public sealed record CreateSectionRequest(Guid ItineraryId, DateOnly Date, string? Description, int? Sequence);

public sealed record UpdateSectionRequest(DateOnly? Date, string? Description, int? Sequence);

public sealed record CreateItemRequest(Guid SectionId, string PlaceId, string? Description, int? Sequence, TimeOnly? StartTime = null);

public sealed record UpdateItemRequest(string? PlaceId, string? Description, int? Sequence, TimeOnly? StartTime = null, bool ClearStartTime = false);
