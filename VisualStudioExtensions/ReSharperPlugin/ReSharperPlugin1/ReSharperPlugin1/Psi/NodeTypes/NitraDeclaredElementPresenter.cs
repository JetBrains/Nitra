using System;
using System.Text;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.Util;

namespace JetBrains.Test
{
  [PsiSharedComponent]
  internal class NitraDeclaredElementPresenter : IDeclaredElementPresenter
  {
    public static NitraDeclaredElementPresenter Instance { get { return PsiShared.GetComponent<NitraDeclaredElementPresenter>(); } }

    public string Format(DeclaredElementPresenterStyle style, IDeclaredElement declaredElement, ISubstitution substitution, out DeclaredElementPresenterMarking marking)
    {
      if (!declaredElement.IsValid())
        throw new ArgumentException("declaredElement should be valid", "declaredElement");

      var nitraDeclaredElement = declaredElement as NitraDeclaredElement;
      if (nitraDeclaredElement == null)
        throw new ArgumentException("declaredElement should have language supported by Nitra", "declaredElement");

      var result = new StringBuilder();
      marking = new DeclaredElementPresenterMarking();

      if (style.ShowEntityKind != EntityKindForm.NONE)
      {
        string entityKind = GetEntityKindStr(nitraDeclaredElement);
        if (entityKind != "")
        {
          if (style.ShowEntityKind == EntityKindForm.NORMAL_IN_BRACKETS)
            entityKind = "(" + entityKind + ")";
          marking.EntityKindRange = AppendString(result, entityKind);
          result.Append(" ");
        }
      }

      if (style.ShowNameInQuotes)
        result.Append("\'");

      if (style.ShowName != NameStyle.NONE)
      {
        var elementName = nitraDeclaredElement.ShortName;

        if (elementName == SharedImplUtil.MISSING_DECLARATION_NAME)
          elementName = "<unknown name>";

        marking.NameRange = AppendString(result, elementName);
      }

      if (style.ShowNameInQuotes)
      {
        if (result[result.Length - 1] == '\'')
          result.Remove(result.Length - 1, 1);
        else
        {
          TrimString(result);
          result.Append("\' ");
        }
      }

      TrimString(result);
      return result.ToString();
    }

    public string Format(ParameterKind parameterKind)
    {
      return String.Empty;
    }

    public string Format(AccessRights accessRights)
    {
      return String.Empty;
    }

    private static string GetEntityKindStr(IDeclaredElement declaredElement)
    {
      return "Nitra declaration";
    }

    private static TextRange AppendString(StringBuilder sb, string substr)
    {
      int s = sb.Length;
      sb.Append(substr);
      return substr.Length == 0 ? TextRange.InvalidRange : new TextRange(s, sb.Length);
    }

    private static void TrimString(StringBuilder str)
    {
      while (str.Length > 0 && str[str.Length - 1] == ' ')
        str.Remove(str.Length - 1, 1);
    }
  }
}