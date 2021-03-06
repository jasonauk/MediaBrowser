﻿using System;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Users;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace MediaBrowser.Controller.Entities.Audio
{
    /// <summary>
    /// Class MusicAlbum
    /// </summary>
    public class MusicAlbum : Folder, IHasAlbumArtist, IHasArtist, IHasMusicGenres, IHasLookupInfo<AlbumInfo>, IMetadataContainer
    {
        public MusicAlbum()
        {
            Artists = new List<string>();
            AlbumArtists = new List<string>();
        }

        [IgnoreDataMember]
        public override bool SupportsAddingToPlaylist
        {
            get { return true; }
        }

        [IgnoreDataMember]
        public MusicArtist MusicArtist
        {
            get
            {
                return GetParents().OfType<MusicArtist>().FirstOrDefault();
            }
        }

        [IgnoreDataMember]
        public List<string> AllArtists
        {
            get
            {
                var list = AlbumArtists.ToList();

                list.AddRange(Artists);

                return list;

            }
        }

        [IgnoreDataMember]
        public string AlbumArtist
        {
            get { return AlbumArtists.FirstOrDefault(); }
        }

        [IgnoreDataMember]
        public override bool SupportsPeople
        {
            get { return false; }
        }

        public List<string> AlbumArtists { get; set; }

        /// <summary>
        /// Gets the tracks.
        /// </summary>
        /// <value>The tracks.</value>
        [IgnoreDataMember]
        public IEnumerable<Audio> Tracks
        {
            get
            {
                return GetRecursiveChildren(i => i is Audio).Cast<Audio>();
            }
        }

        protected override IEnumerable<BaseItem> GetEligibleChildrenForRecursiveChildren(User user)
        {
            return Tracks;
        }

        public List<string> Artists { get; set; }

        /// <summary>
        /// Gets the user data key.
        /// </summary>
        /// <returns>System.String.</returns>
        protected override string CreateUserDataKey()
        {
            var id = this.GetProviderId(MetadataProviders.MusicBrainzReleaseGroup);

            if (!string.IsNullOrWhiteSpace(id))
            {
                return "MusicAlbum-MusicBrainzReleaseGroup-" + id;
            }

            id = this.GetProviderId(MetadataProviders.MusicBrainzAlbum);

            if (!string.IsNullOrWhiteSpace(id))
            {
                return "MusicAlbum-Musicbrainz-" + id;
            }

            return base.CreateUserDataKey();
        }

        protected override bool GetBlockUnratedValue(UserPolicy config)
        {
            return config.BlockUnratedItems.Contains(UnratedItem.Music);
        }

        public override UnratedItem GetBlockUnratedType()
        {
            return UnratedItem.Music;
        }

        public AlbumInfo GetLookupInfo()
        {
            var id = GetItemLookupInfo<AlbumInfo>();

            id.AlbumArtists = AlbumArtists;

            var artist = GetParents().OfType<MusicArtist>().FirstOrDefault();

            if (artist != null)
            {
                id.ArtistProviderIds = artist.ProviderIds;
            }

            id.SongInfos = GetRecursiveChildren(i => i is Audio)
                .Cast<Audio>()
                .Select(i => i.GetLookupInfo())
                .ToList();

            var album = id.SongInfos
                .Select(i => i.Album)
                .FirstOrDefault(i => !string.IsNullOrWhiteSpace(i));

            if (!string.IsNullOrWhiteSpace(album))
            {
                id.Name = album;
            }

            return id;
        }

        public async Task RefreshAllMetadata(MetadataRefreshOptions refreshOptions, IProgress<double> progress, CancellationToken cancellationToken)
        {
            var items = GetRecursiveChildren().ToList();

            var songs = items.OfType<Audio>().ToList();

            var others = items.Except(songs).ToList();

            var totalItems = songs.Count + others.Count;
            var numComplete = 0;

            var childUpdateType = ItemUpdateType.None;

            // Refresh songs
            foreach (var item in songs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var updateType = await item.RefreshMetadata(refreshOptions, cancellationToken).ConfigureAwait(false);
                childUpdateType = childUpdateType | updateType;

                numComplete++;
                double percent = numComplete;
                percent /= totalItems;
                progress.Report(percent * 100);
            }

            var parentRefreshOptions = refreshOptions;
            if (childUpdateType > ItemUpdateType.None)
            {
                parentRefreshOptions = new MetadataRefreshOptions(refreshOptions);
                parentRefreshOptions.MetadataRefreshMode = MetadataRefreshMode.FullRefresh;
            }

            // Refresh current item
            await RefreshMetadata(parentRefreshOptions, cancellationToken).ConfigureAwait(false);

            // Refresh all non-songs
            foreach (var item in others)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var updateType = await item.RefreshMetadata(parentRefreshOptions, cancellationToken).ConfigureAwait(false);

                numComplete++;
                double percent = numComplete;
                percent /= totalItems;
                progress.Report(percent * 100);
            }

            progress.Report(100);
        }
    }
}
