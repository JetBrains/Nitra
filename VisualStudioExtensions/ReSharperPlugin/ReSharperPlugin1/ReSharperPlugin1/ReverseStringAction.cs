using System;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.Bulbs;
using JetBrains.ReSharper.Feature.Services.LinqTools;
using JetBrains.ReSharper.Intentions.Extensibility;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace ReSharperPlugin1
{
  /// <summary>
  /// This is an example context action. The test project demonstrates tests for
  /// availability and execution of this action.
  /// </summary>
  [ContextAction(Name = "ReverseString", Description = "Reverses a string", Group = "C#")]
  public class ReverseStringAction : ContextActionBase
  {
    private readonly ICSharpContextActionDataProvider _provider;
    private ILiteralExpression _stringLiteral;

    public ReverseStringAction(ICSharpContextActionDataProvider provider)
    {
      _provider = provider;
    }

    public override bool IsAvailable(IUserDataHolder cache)
    {
      var literal = _provider.GetSelectedElement<ILiteralExpression>(true, true);
      if (literal != null && literal.IsConstantValue() && literal.ConstantValue.IsString())
      {
        var s = literal.ConstantValue.Value as string;
        if (!string.IsNullOrEmpty(s))
        {
          _stringLiteral = literal;
          return true;
        }
      }
      return false;
    }

    protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
    {
      CSharpElementFactory factory = CSharpElementFactory.GetInstance(_provider.PsiModule);

      var stringValue = _stringLiteral.ConstantValue.Value as string;
      if (stringValue == null)
        return null;

      var chars = stringValue.ToCharArray();
      Array.Reverse(chars);
      ICSharpExpression newExpr = factory.CreateExpressionAsIs("\"" + new string(chars) + "\"");
      _stringLiteral.ReplaceBy(newExpr);
      return null;
    }

    public override string Text
    {
      get { return "Reverse string"; }
    }
  }
}