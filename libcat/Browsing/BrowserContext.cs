using CatCommander.Resources;

namespace CatCommander.Browsing;

/// <summary>Immutable description of one tab's current listing and navigation semantics.</summary>
public sealed record BrowserContext(IListingSource Listing)
{
    public ListingKind Kind => Listing.Kind;
    public ResourceRef? Location => Listing.Location;
    public ContainerRef? WritableDestination => Listing.WritableDestination;

    public ResourceRef? GetBackTarget(BrowserItem? currentItem) =>
        Listing.Navigation.GetBackTarget(Listing, currentItem);

    public ResourceRef? GetBackSelection(BrowserItem? currentItem) =>
        Listing.Navigation.GetBackSelection(Listing, currentItem);
}
