using System;
using System.IO;
using System.Threading.Tasks;

namespace Sandbox.UI;

[Alias( "iconify", "iconify-icon" )]
public class IconifyPanel : Panel
{
	private Texture _svgTexture;

	private bool _dirty = false;
	private string _icon = "";

	public string Icon
	{
		get => _icon;
		set
		{
			if ( _icon == value )
				return;

			_icon = value;
			_dirty = true;
		}
	}

	public IconifyPanel()
	{
		StyleSheet.Parse( """
		IconifyPanel, iconify, iconify-icon {
			height: 16px;
			aspect-ratio: 1;
			align-self: center;
		    padding: 0 8px;
		}

		IconifyPanel:first-child, iconify:first-child {
		    padding-left: 0;
		}

		IconifyPanel:last-child, iconify:last-child {
		    padding-right: 0;
		}
		""" );
	}

	private (string Pack, string Name) ParseIcon( string icon )
	{
		if ( !icon.Contains( ':' ) )
			throw new ArgumentException( $"Icon must be in the format 'pack:name', got '{icon}'" );

		var splitName = icon.Split( ':', StringSplitOptions.RemoveEmptyEntries );

		if ( splitName.Length != 2 )
			throw new ArgumentException( $"Icon must be in the format 'pack:name', got '{icon}'" );

		var pack = splitName[0].Trim();
		var name = splitName[1].Trim();

		return (pack, name);
	}

	protected override void OnAfterTreeRender( bool firstTime )
	{
		SetIcon();
	}

	public override void OnLayout( ref Rect layoutRect )
	{
		_dirty = true;
	}

	public override void SetProperty( string name, string value )
	{
		base.SetProperty( name, value );
		
		if ( name.Equals( "icon", StringComparison.OrdinalIgnoreCase ) || name.Equals( "name", StringComparison.OrdinalIgnoreCase ) )
			Icon = value;
	}

	public override void DrawBackground( ref RenderState state )
	{
		base.DrawBackground( ref state );

		Graphics.Attributes.Set( "LayerMat", Matrix.Identity );
		Graphics.Attributes.Set( "Texture", _svgTexture );
		Graphics.Attributes.SetCombo( "D_BLENDMODE", BlendMode.Normal );
		Graphics.DrawQuad( Box.Rect, Material.UI.Basic, Color.White );
	}

	/// <summary>
	/// Fetches the icon - if it doesn't exist on disk, it will fetch it for you.
	/// </summary>
	private async Task<string> FetchIconAsync( string iconPath )
	{
		var (pack, name) = ParseIcon( iconPath );
		var localPath = $"iconify/{pack}/{name}.svg";

		if ( !FileSystem.Data.FileExists( localPath ) )
		{
			Log.Info( $"Cache miss for icon '{iconPath}', fetching from API..." );

			var directory = Path.GetDirectoryName( localPath );
			FileSystem.Data.CreateDirectory( directory );

			var remotePath = $"https://api.iconify.design/{pack}/{name}.svg";
			var response = await Http.RequestAsync( "GET", remotePath );
			var iconContents = await response.Content.ReadAsStringAsync();
			iconContents = iconContents.Replace( " width=\"1em\" height=\"1em\"", "" ); // HACK

			// this API doesn't actually return a 404 status code, so check the document for '404' itself...
			if ( iconContents == "404" )
			{
				Log.Error( $"Failed to fetch icon {iconPath}" );
				return "";
			}

			FileSystem.Data.WriteAllText( localPath, iconContents );
		}

		return localPath;
	}

	private void SetIcon()
	{
		if ( !_dirty )
			return;

		_dirty = false;
		_svgTexture = Texture.White;

		FetchIconAsync( Icon ).ContinueWith( task =>
		{
			var basePath = task.Result;
			if ( string.IsNullOrEmpty( basePath ) )
				return;

			Log.Info( $"Fetched {basePath}" );

			var color = Parent?.ComputedStyle?.FontColor?.Hex ?? "#ffffff";
			var width = Box.Rect.Width;
			var height = Box.Rect.Height;
			var pathParams = $"?color={color}&w={width}&h={height}";

			var path = basePath + pathParams;
			_svgTexture = Texture.Load( FileSystem.Data, path );
		} );
	}
}
