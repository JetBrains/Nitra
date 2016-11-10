using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using Nitra.ClientServer.Messages;
using Nitra.ClientServer.Client;

namespace Nitra.VisualStudio
{
  /// <summary>
  /// Это одна строка в окошке Find All References. От LibraryNode
  /// отличается тем что содержит ссылку на GotoInfo, форматирует своё описание
  /// в стиле соответствующем стилю C#, и поддерживает переход на Location найденной ссылки
  /// </summary>
  /// <remarks>
  /// Пока тип найденной записи не анализируется, из-за этого нет нормальной картинки
  /// </remarks>
  class GotoInfoLibraryNode : LibraryNode
  {
    private readonly Location _location;
    private readonly Server _server;
    private readonly StringManager _stringManager;
    private readonly string _caption;
    private readonly string Text;

    public GotoInfoLibraryNode(Location location, string caption, Server server)
        : base(caption)
    {
      _location = location;
      _server = server;
      _caption = caption;
      CanGoToSource = true;

      Text = location.Range.Text;
    }

    public string Path { get { return _server.Client.StringManager.GetPath(_location.File.FileId); } }

    protected override void GotoSource(Microsoft.VisualStudio.Shell.Interop.VSOBJGOTOSRCTYPE gotoType)
    {
      _server.ServiceProvider.Navigate(Path, _location.Range.StartLine, _location.Range.StartColumn);
    }

    protected override uint CategoryField(LIB_CATEGORY category)
    {
      return (uint)LibraryNodeType.None;
    }

    protected override string GetTextWithOwnership(VSTREETEXTOPTIONS tto)
    {
      if (tto == VSTREETEXTOPTIONS.TTO_DEFAULT)
      {
        var r = _location.Range;
        // TODO: use SpanshotSpan if Spanshot avalable.
        return $"{Name} {Path} - ({r.StartLine}, {r.StartColumn}) : {Text}";
      }

      return null;
    }
  }
}
