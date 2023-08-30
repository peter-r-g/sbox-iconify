using Iconify;
using Iconify.Util;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Sandbox.UI;

public struct IconifyIcon
{
	public string Prefix { get; private set; }
	public string Name { get; private set; }

	public readonly bool IsTintable => IconifyOptions.Current.CacheFileSystem.FileExists( LocalTintablePath );

	private readonly string WidthParam => HttpUtility.UrlEncode( "width=100%" );
	private readonly string Url => $"https://api.iconify.design/{Prefix}/{Name}.svg?{WidthParam}";
	private readonly string LocalPath => $"{Prefix}/{Name}.svg";
	private readonly string LocalTintablePath => $"{Prefix}/{Name}.t.svg";

	private static readonly ConcurrentHashSet<string> _fetchingImages = new();

	private async Task<string> FetchImageDataAsync()
	{
		var response = await Http.RequestAsync( Url, "GET" );
		var iconContents = await response.Content.ReadAsStringAsync();

		// this API doesn't actually return a 404 status code :( check the document for '404' itself...
		if ( response.StatusCode == HttpStatusCode.NotFound || iconContents == "404" )
			throw new Exception( $"Failed to fetch icon {this}" );
		
		return iconContents;
	}

	public async Task EnsureIconDataIsCachedAsync( BaseFileSystem fs )
	{
		if ( fs.FileExists( LocalPath ) )
			return;

		if ( _fetchingImages.Contains( LocalPath ) )
		{
			do
			{
				await GameTask.Delay( 1 );
			} while ( _fetchingImages.Contains( LocalPath ) );

			return;
		}

		try
		{
			_fetchingImages.Add( LocalPath );

			var directory = Path.GetDirectoryName( LocalPath );
			fs.CreateDirectory( directory );

			var iconContents = await FetchImageDataAsync();
			// HACK: Check whether this icon is tintable based on whether it references CSS currentColor
			var isTintable = iconContents.Contains( "currentColor" );
			fs.WriteAllText( isTintable ? LocalTintablePath : LocalPath, iconContents );
		}
		finally
		{
			_fetchingImages.Remove( LocalPath );
		}
	}

	public async Task<Texture> LoadTextureAsync( Rect rect, Color? tintColor, CancellationToken cancellationToken = default )
	{
		var fs = IconifyOptions.Current.CacheFileSystem;
		// NOTE: Not passing the cancellation token here so that the icon can still be cached for later.
		await EnsureIconDataIsCachedAsync( fs );
		cancellationToken.ThrowIfCancellationRequested();

		var pathParams = BuildPathParams( rect, tintColor );
		var path = (IsTintable ? LocalTintablePath : LocalPath) + pathParams;

		var texture = await Texture.LoadAsync( fs, path );
		cancellationToken.ThrowIfCancellationRequested();

		return texture;
	}

	private string BuildPathParams( Rect rect, Color? tintColor )
	{
		var pathParamsBuilder = new StringBuilder( "?" );

		if ( IsTintable && tintColor.HasValue )
			pathParamsBuilder.Append( $"color={tintColor.Value.Hex}&" );

		var width = Math.Max( 32, rect.Width );
		var height = Math.Max( 32, rect.Height );

		pathParamsBuilder.Append( $"w={width}&h={height}" );
		return pathParamsBuilder.ToString();
	}

	public IconifyIcon( string path )
	{
		if ( !path.Contains( ':' ) )
			throw new ArgumentException( $"Icon must be in the format 'prefix:name', got '{path}'" );

		var splitName = path.Split( ':', StringSplitOptions.RemoveEmptyEntries );

		if ( splitName.Length != 2 )
			throw new ArgumentException( $"Icon must be in the format 'prefix:name', got '{path}'" );

		Prefix = splitName[0].Trim();
		Name = splitName[1].Trim();
	}

	public static implicit operator IconifyIcon( string path ) => new IconifyIcon( path );

	public override string ToString()
	{
		return $"{Prefix}:{Name}";
	}
}
