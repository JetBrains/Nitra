using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Tools;
using CreatePkgDefImpl = Microsoft.VisualStudio.Tools.CreatePkgDef;

namespace Nemerle.Build.Tasks
{
  public class CreatePkgDef : Task, ITask
  {
    public string       ProductVersion        { get; set; }
    public ITaskItem    AssemblyToProcess     { get; set; }
    public string       SDKVersion            { get; set; }
    public bool         UseCodebase           { get; set; }
    public ITaskItem[]  ReferencedAssemblies  { get; set; }
    public string       OutputFile            { get; set; }
    public bool         IsVerbose             { get; set; }

    public override bool Execute()
    {
      var otherDomain = AppDomain.CreateDomain("other domain");

      var otherType = typeof(OtherProgram);
      var obj = otherDomain.CreateInstanceFromAndUnwrap(
                               GetAssemblyPath(otherType.Assembly),
                               otherType.FullName) as OtherProgram;

      Trace.Assert(obj != null);

      var msg = obj.Main(
        AssemblyToProcess.ToString(),
        UseCodebase, ReferencedAssemblies == null ? new string[]{} : ReferencedAssemblies.Select(x => x.ToString()).ToArray(),
        OutputFile,
        IsVerbose);

      if (msg != null)
      {
        this.Log.LogMessage(MessageImportance.High, "CreatePkgDef task fail: " + msg);
        return false;
      }

      return true;
    }

    public static string GetAssemblyPath(Assembly assembly)
    {
      var codeBase = assembly.CodeBase;
      var uri = new UriBuilder(codeBase);
      var path = Uri.UnescapeDataString(uri.Path);
      return path;
    }
  }

  public class OtherProgram : MarshalByRefObject
  {
    public string Main(string assemblyToProcess, bool useCodebase, string[] referencedAssemblies, string outputFile, bool isVerbose)
    {
      //Console.WriteLine(AppDomain.CurrentDomain.FriendlyName);
      InputArguments inputArguments = InputArguments.ParseArgs(new string[] { });
      inputArguments.isVerbose = isVerbose;
      inputArguments.includedAssemblies = new List<string>(referencedAssemblies);
      inputArguments.fileName = assemblyToProcess;
      inputArguments.mode = RegistrationMode.PkgDef;
      inputArguments.registrationMethod = useCodebase ? RegistrationMethod.CodeBase : RegistrationMethod.Assembly;
      inputArguments.outputFile = outputFile;
      lock (this)
      {
        try
        {
          var content = CreatePkgDefImpl.DoCreatePkgDef(inputArguments);
          File.WriteAllText(outputFile, content, Encoding.Unicode);
        }
        catch (Exception ex)
        {
          return ex.Message;
        }

        return null;
      }
    }
  }
}
