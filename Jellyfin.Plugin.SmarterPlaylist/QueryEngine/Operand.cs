using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine
{
    /// <summary>
    /// The flattened view of a library item that playlist rules are evaluated against.
    /// </summary>
    /// <remarks>
    /// Property names on this type are the vocabulary available to <see cref="Expression.MemberName"/>
    /// in playlist JSON. Adding a property here makes it filterable; renaming one is a breaking change
    /// for existing playlist files.
    /// </remarks>
    /// <param name="name">Display name of the underlying library item.</param>
    public class Operand(string name)
    {
        /// <summary>
        /// Gets the names of people credited as actors.
        /// </summary>
        public Collection<string> Actors { get; } = [];

        /// <summary>
        /// Gets the names of people credited as composers.
        /// </summary>
        public Collection<string> Composers { get; } = [];

        /// <summary>
        /// Gets or sets the community rating, or zero when the item has none.
        /// </summary>
        public float CommunityRating { get; set; }

        /// <summary>
        /// Gets or sets the critic rating, or zero when the item has none.
        /// </summary>
        public float CriticRating { get; set; }

        /// <summary>
        /// Gets the names of people credited as directors.
        /// </summary>
        public Collection<string> Directors { get; } = [];

        /// <summary>
        /// Gets the genres assigned to the item.
        /// </summary>
        public Collection<string> Genres { get; } = [];

        /// <summary>
        /// Gets the names of people credited as guest stars.
        /// </summary>
        public Collection<string> GuestStars { get; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether the owning user has played this item.
        /// </summary>
        public bool IsPlayed { get; set; }

        /// <summary>
        /// Gets or sets the display name of the item.
        /// </summary>
        public string Name { get; set; } = name;

        /// <summary>
        /// Gets or sets the path of the folder containing the item's media file.
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the release date in Unix seconds, or zero when the item has none.
        /// </summary>
        public double PremiereDate { get; set; }

        /// <summary>
        /// Gets the names of people credited as producers.
        /// </summary>
        public Collection<string> Producers { get; } = [];

        /// <summary>
        /// Gets the studios associated with the item.
        /// </summary>
        public Collection<string> Studios { get; } = [];

        /// <summary>
        /// Gets the names of people credited as writers.
        /// </summary>
        public Collection<string> Writers { get; } = [];

        /// <summary>
        /// Gets or sets the item's media type, such as <c>Video</c> or <c>Audio</c>.
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the album the item belongs to, if any.
        /// </summary>
        public string Album { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the show an episode belongs to, or empty for anything else.
        /// </summary>
        /// <remarks>
        /// This is what makes a franchise selectable. Matching every Star Trek series means a rule on
        /// this member rather than on the folder path, which only works if the library happens to be
        /// organised that way.
        /// </remarks>
        public string SeriesName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the season an episode belongs to, or empty for anything else.
        /// </summary>
        public string SeasonName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the season number of an episode, or zero when it has none.
        /// </summary>
        public int SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode number within its season, or zero when it has none.
        /// </summary>
        public int EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets the year the item was produced, or zero when unknown.
        /// </summary>
        public int ProductionYear { get; set; }

        /// <summary>
        /// Gets or sets the official rating, such as a content certificate, or empty when unset.
        /// </summary>
        public string OfficialRating { get; set; } = string.Empty;

        /// <summary>
        /// Gets the tags assigned to the item.
        /// </summary>
        public Collection<string> Tags { get; } = [];

        /// <summary>
        /// Gets or sets the runtime in minutes, or zero when unknown.
        /// </summary>
        public double RunTimeMinutes { get; set; }

        /// <summary>
        /// Gets or sets the time the item was added to the library, in Unix seconds.
        /// </summary>
        public double DateCreated { get; set; }

        /// <summary>
        /// Gets or sets the time the item's metadata was last refreshed, in Unix seconds.
        /// </summary>
        public double DateLastRefreshed { get; set; }

        /// <summary>
        /// Gets or sets the time the item was last saved, in Unix seconds.
        /// </summary>
        public double DateLastSaved { get; set; }

        /// <summary>
        /// Gets or sets the time the item's media file was last modified, in Unix seconds.
        /// </summary>
        public double DateModified { get; set; }
    }
}
