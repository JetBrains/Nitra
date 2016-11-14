using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace EPiServer.Labs.LangFilesExtension.Core.QuickInfo
{
  internal class NitraQuickInfoSource : IQuickInfoSource
  {
    [Import] internal IClassificationTypeRegistryService ClassificationTypeRegistryService { get; set; }

    readonly ITextBuffer            _buffer;
    readonly NitraQuickInfoProvider _provider;

    bool _isDisposed;

    public NitraQuickInfoSource(NitraQuickInfoProvider provider, ITextBuffer buffer)
    {
      _provider = provider;
      _buffer = buffer;
    }

    #region IQuickInfoSource Members

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <filterpriority>2</filterpriority>
    public void Dispose()
    {
      if (!_isDisposed)
      {
        GC.SuppressFinalize(this);
        _isDisposed = true;
      }
    }

    /// <summary>
    /// Determines which pieces of QuickInfo content should be part of the specified <see cref="T:Microsoft.VisualStudio.Language.Intellisense.IQuickInfoSession"/>.
    /// </summary>
    /// <param name="session">The session for which completions are to be computed.</param>
    /// <param name="quickInfoContent">The QuickInfo content to be added to the session.</param>
    /// <param name="applicableToSpan">The <see cref="T:Microsoft.VisualStudio.Text.ITrackingSpan"/> to which this session applies.</param>
    /// <remarks>
    /// Each applicable <see cref="M:Microsoft.VisualStudio.Language.Intellisense.IQuickInfoSource.AugmentQuickInfoSession(Microsoft.VisualStudio.Language.Intellisense.IQuickInfoSession,System.Collections.Generic.IList{System.Object},Microsoft.VisualStudio.Text.ITrackingSpan@)"/> instance will be called in-order to (re)calculate a
    ///             <see cref="T:Microsoft.VisualStudio.Language.Intellisense.IQuickInfoSession"/>. Objects can be added to the session by adding them to the quickInfoContent collection
    ///             passed-in as a parameter.  In addition, by removing items from the collection, a source may filter content provided by
    ///             <see cref="T:Microsoft.VisualStudio.Language.Intellisense.IQuickInfoSource"/>s earlier in the calculation chain.
    /// </remarks>
    public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
    {
      applicableToSpan = null;
      // Map the trigger point down to our buffer.
      SnapshotPoint? triggerPointOpt = session.GetTriggerPoint(_buffer.CurrentSnapshot);
      if (!triggerPointOpt.HasValue)
        return;

      var triggerPoint = triggerPointOpt.Value;

      quickInfoContent.Insert(0, "тест! тест! Тест!!!");

      applicableToSpan = triggerPoint.Snapshot.CreateTrackingSpan(triggerPoint.Position - 5, 10, SpanTrackingMode.EdgeExclusive);
    }

    #endregion
  }
}
