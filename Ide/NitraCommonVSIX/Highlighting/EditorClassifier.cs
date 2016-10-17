//------------------------------------------------------------------------------
// <copyright file="EditorClassifier.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System.Diagnostics;
using Nitra.ClientServer.Messages;
using System.Collections.Immutable;

namespace Nitra.VisualStudio.Highlighting
{
  /// <summary>
  /// Classifier that classifies all text as an instance of the "EditorClassifier" classification type.
  /// </summary>
  internal class NitraEditorClassifier : IClassifier
  {
    /// <summary>
    /// Classification type.
    /// </summary>
    private readonly IClassificationType                  _classificationType;
    private readonly ITextBuffer                          _buffer;
    private readonly IClassificationTypeRegistryService   _registry;
    private readonly Dictionary<int, IClassificationType> _classificationMap = new Dictionary<int, IClassificationType>();

    private Server                     _server;
    private ImmutableArray<SpanInfo>[] _spanInfos = new ImmutableArray<SpanInfo>[(int)HighlightingType.Count];
    private ITextSnapshot[]            _snapshots = new ITextSnapshot[(int)HighlightingType.Count];

    /// <summary>
    /// Initializes a new instance of the <see cref="NitraEditorClassifier"/> class.
    /// </summary>
    /// <param name="registry">Classification registry.</param>
    internal NitraEditorClassifier(IClassificationTypeRegistryService registry, ITextBuffer buffer)
    {
      var currentSnapshot = buffer.CurrentSnapshot;
      _buffer             = buffer;
      _registry           = registry;
      _classificationType = registry.GetClassificationType("EditorClassifier");

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
        if (_classificationMap.Count == 0 && _classificationMap.Count < Server.SpanClassInfos.Length)
        {
          _classificationMap.Clear();

          foreach (var info in Server.SpanClassInfos)
          {
            var name = info.FullName.Replace(".", "_");
            var ct = _registry.GetClassificationType(name);

            if (ct == null)
            {
              Debug.WriteLine($"No ClassificationType for '{info.FullName}' SpanClassInfo.");
              ct = _registry.CreateTransientClassificationType(new[] { _classificationType });
            }

            _classificationMap.Add(info.Id, ct);
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

    /// <summary>
    /// Gets all the <see cref="ClassificationSpan"/> objects that intersect with the given range of text.
    /// </summary>
    /// <remarks>
    /// This method scans the given SnapshotSpan for potential matches for this classification.
    /// In this instance, it classifies everything and returns each span as a new ClassificationSpan.
    /// </remarks>
    /// <param name="processedSpan">The span currently being classified.</param>
    /// <returns>A list of ClassificationSpans that represent spans identified to be of this classification.</returns>
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

    internal void Update(HighlightingType highlightingType, ImmutableArray<SpanInfo> spanInfos, int version)
    {
      if (ClassificationChanged == null)
        return;

      var snapshot = _buffer.CurrentSnapshot;

      if (snapshot.Version.VersionNumber != version + 1)
        return;

      _snapshots[(int)highlightingType] = snapshot;
      _spanInfos[(int)highlightingType] = spanInfos;
      ClassificationChanged(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
    }

    #endregion
  }
}
