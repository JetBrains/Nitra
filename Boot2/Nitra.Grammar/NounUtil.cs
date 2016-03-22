using System;
using System.Collections.Generic;
using System.IO;

namespace JetBrains.Util
{
  public static class NounUtil
  {
    private static readonly IDictionary<string, string> ourPlurals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly IDictionary<string, string> ourSingulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    static NounUtil()
    {
      using (var streamReader = new StreamReader(typeof(NounUtil).Assembly.GetManifestResourceStream("Nitra.Grammar.resources.plural.txt")))
      {
        while (true)
        {
          string line = streamReader.ReadLine();

          if (line == null)
            return;

          string singular = line.Substring(0, line.IndexOf(" ", StringComparison.Ordinal));
          string plural = line.Substring(line.IndexOf(" ", StringComparison.Ordinal) + 1);

          if (!ourPlurals.ContainsKey(singular))
            ourPlurals.Add(singular, plural);

          if (!ourSingulars.ContainsKey(plural))
            ourSingulars.Add(plural, singular);
        }
      }
    }

    public static string GetPlural(string singular)
    {
      string plural;


      if (ourPlurals.TryGetValue(singular, out plural))
        return plural;

      if (singular.EndsWith("s") || singular.EndsWith("x") || singular.EndsWith("sh")|| singular.EndsWith("ch"))
        return singular + "es";

      if (singular.EndsWith("y"))
        return singular.Substring(0, singular.Length - 1) + "ies";

      return singular + "s";
    }

    public static string GetSingular(string plural)
    {
      string singular;

      if (ourSingulars.TryGetValue(plural, out singular))
        return singular;

      if (!plural.EndsWith("s") || plural.EndsWith("ss") || plural.EndsWith("xs") || plural.EndsWith("shs"))
        return plural;

      if (plural.EndsWith("ies"))
        return plural.Substring(0, plural.Length - 3) + "y";

      if ( plural.EndsWith("sses") || plural.EndsWith("zes") || plural.EndsWith("xes") || plural.EndsWith("shes")|| plural.EndsWith("ches"))
        return plural.Substring(0, plural.Length - 2);

      return plural.Substring(0, plural.Length - 1);
    }

    public static string ToPluralOrSingular(string singular, int count)
    {
      if(singular == null)
        throw new ArgumentNullException("singular");
      return count == 1 || count == -1 ? singular : GetPlural(singular);
    }

    public static string GetCountString(int argumentIndex)
    {
      string suffix = "th";
      switch (argumentIndex + 1)
      {
        case 1:
          suffix = "st";
          break;
        case 2:
          suffix = "nd";
          break;
        case 3:
          suffix = "d";
          break;
      }
      return argumentIndex + 1 + suffix;
    }
  }
}
