using Nemerle.Utility;

using System.Collections.Generic;

namespace DescribeBehaviour
{
  [Record] // macro which automatically create constructor for all fields
  public class Feature
  {
    public string                Content   { get; private set; }
    public string                AsA       { get; private set; }
    public string                IWantTo   { get; private set; }
    public string                SoThat    { get; private set; }
    public IEnumerable<Scenario> Scenarios { get; private set; }
  }

  [Record] // macro which automatically create constructor for all fields
  public class Scenario
  {
    public string Content { get; private set; }
    public string Given   { get; private set; }
    public string When    { get; private set; }
    public string Then    { get; private set; }
  }
}
