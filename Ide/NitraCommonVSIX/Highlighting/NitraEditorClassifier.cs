using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

using Nitra.ClientServer.Messages;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;

namespace Nitra.VisualStudio.Highlighting
{
  internal class NitraEditorClassifier : IClassifier
  {
    readonly IClassificationType                  _classificationType;
    readonly ITextBuffer                          _buffer;
    readonly IClassificationTypeRegistryService   _registry;
    readonly Dictionary<int, IClassificationType> _classificationMap = new Dictionary<int, IClassificationType>();
    readonly ImmutableArray<SpanInfo>[]           _spanInfos = new ImmutableArray<SpanInfo>[(int)HighlightingType.Count];
    readonly ITextSnapshot[]                      _snapshots = new ITextSnapshot[(int)HighlightingType.Count];
    readonly IClassificationFormatMapService      _classificationFormatMapService;
             Server                               _server;

    public NitraEditorClassifier(IClassificationTypeRegistryService registry, IClassificationFormatMapService formatMapService, ITextBuffer buffer)
    {
      var currentSnapshot             = buffer.CurrentSnapshot;
      _registry                       = registry;
      _classificationFormatMapService = formatMapService;
      _buffer                         = buffer;
      _classificationType             = registry.GetClassificationType("EditorClassifier");

      for (int i = 0; i < _spanInfos.Length; i++)
      {
        _spanInfos[i] = ImmutableArray<SpanInfo>.Empty;
        _snapshots[i] = currentSnapshot;
      }
    }

    private Server Server
    {
      get
      {
        if (_server == null)
          _server = (Server)_buffer.Properties.GetProperty(Constants.ServerKey);

        return _server;
      }
    }

    private Dictionary<int, IClassificationType> ClassificationMap
    {
      get
      {
        if (_classificationMap.Count == 0 || _classificationMap.Count < Server.SpanClassInfos.Length)
        {
          _classificationMap.Clear();
          IClassificationFormatMap classificationFormatMap = null;

          foreach (var info in Server.SpanClassInfos)
          {
            var name               = info.FullName;
            var classificationType = _registry.GetClassificationType(name);

            if (classificationType == null)
            {
              // create temporary ClassificationType and define it format
              Debug.WriteLine($"No ClassificationType for '{info.FullName}' SpanClassInfo.");
              classificationType = _registry.CreateClassificationType(name, new[] { _classificationType });

              if (classificationFormatMap == null)
                classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap("nitra");

              var identifierProperties = classificationFormatMap.GetExplicitTextProperties(classificationType);

              // modify the properties
              var bytes         = BitConverter.GetBytes(info.ForegroundColor);
              var color         = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
              var newProperties = identifierProperties.SetForeground(color);

              classificationFormatMap.AddExplicitTextProperties(classificationType, newProperties);
            }

            _classificationMap.Add(info.Id, classificationType);
          }

        }
        return _classificationMap;
      }
    }

    #region IClassifier

#pragma warning disable 67

    /// <summary>
    /// An event that occurs when the classification of a span of text has changed.
    /// </summary>
    /// <remarks>
    /// This event gets raised if a non-text change would affect the classification in some way,
    /// for example typing /* would cause the classification to change in C# without directly
    /// affecting the span.
    /// </remarks>
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

#pragma warning restore 67

    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan processedSpan)
    {
      var currentSnapshot = processedSpan.Snapshot;
      var result = new List<ClassificationSpan>();

      for (int i = 0; i < _spanInfos.Length; i++)
      {
        var snapshot  = _snapshots[i];
        var spanInfos = _spanInfos[i];
        var translatesSnapshot = processedSpan.TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
        var processedSpanInfo = new SpanInfo(new NSpan(translatesSnapshot.Span.Start, translatesSnapshot.Span.End), -1);
        var index = spanInfos.BinarySearch(processedSpanInfo, SpanInfo.Comparer);
        if (index < 0)
          index = ~index;

        for (int k = index; k < spanInfos.Length; k++)
        {
          var spanInfo = spanInfos[k];
          var span     = spanInfo.Span;
          var newSpan  = new SnapshotSpan(snapshot, new Span(span.StartPos, span.Length))
                              .TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);

          if (!newSpan.IntersectsWith(processedSpan))
            break;

          var classificationType = ClassificationMap[spanInfo.SpanClassId];
          result.Add(new ClassificationSpan(newSpan, classificationType));
        }
      }

      return result;
    }

    internal void Update(HighlightingType highlightingType, ImmutableArray<SpanInfo> spanInfos, FileVersion version)
    {
      if (ClassificationChanged == null)
        return;

      var snapshot = _buffer.CurrentSnapshot;

      if (snapshot.Version.Convert() != version)
        return;

      _snapshots[(int)highlightingType] = snapshot;
      _spanInfos[(int)highlightingType] = spanInfos;
      ClassificationChanged(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
    }

    internal Brush SpanClassToBrush(string spanClass, IWpfTextView _wpfTextView)
    {
      var spanClassOpt = Server.GetSpanClassOpt(spanClass);
      if (!spanClassOpt.HasValue)
        return Server.SpanClassToBrush(spanClass);

      IClassificationType classificationType;
      if (_classificationMap.TryGetValue(spanClassOpt.Value.Id, out classificationType))
      {
        var map = _classificationFormatMapService.GetClassificationFormatMap(_wpfTextView);
        var properties = map.GetTextProperties(classificationType);
        return properties.ForegroundBrush;
      }

      return Server.SpanClassToBrush(spanClass);
    }

    #endregion
  }
}
