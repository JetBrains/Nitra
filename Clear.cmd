set RemoveCmd=rmdir /S /Q 
%RemoveCmd% bin\
%RemoveCmd% Grammars\Bin\
%RemoveCmd% Grammars\CSharp\CSharp.Grammar\obj\
%RemoveCmd% Grammars\Json\Json.Grammar\obj\
%RemoveCmd% Grammars\Json\Tests\Sample.Json.Cs\bin\
%RemoveCmd% Grammars\Json\Tests\Sample.Json.Cs\obj\
%RemoveCmd% Grammars\Json\Tests\Sample.Json.Nemerle\bin\
%RemoveCmd% Grammars\Samples\Calculator\Sample.Calc\obj\
%RemoveCmd% Grammars\Samples\Calculator\Sample.Calc.App\bin\
%RemoveCmd% Grammars\Samples\Calculator\Sample.Calc.App\obj\
%RemoveCmd% Grammars\Samples\Calculator\Sample.Calc.Quotation\bin\
%RemoveCmd% Grammars\Samples\Calculator\Sample.Calc.Quotation\obj\
%RemoveCmd% Grammars\Samples\Calculator\Sample.Num\obj\
%RemoveCmd% Nitra\DotNetLang\bin\
%RemoveCmd% Nitra\DotNetLang\obj\
%RemoveCmd% Nitra\Nitra\bin\
%RemoveCmd% Nitra\Nitra\obj\
%RemoveCmd% Nitra\Nitra.Compiler\obj\
%RemoveCmd% Nitra\Nitra.Grammar\bin\
%RemoveCmd% Nitra\Nitra.Grammar\obj\
%RemoveCmd% Nitra\Nitra.Runtime\bin\
%RemoveCmd% Nitra\Nitra.Runtime\obj\
%RemoveCmd% Nitra\Nitra.Runtime.Macros\bin\
%RemoveCmd% Nitra\Nitra.Runtime.Macros\obj\
%RemoveCmd% Nitra.LanguageCompiler\bin\
%RemoveCmd% Nitra.LanguageCompiler\obj\
%RemoveCmd% Nitra.LanguageCompiler\Templates\XXLanguageXXVsPackage\bin\
%RemoveCmd% Nitra.LanguageCompiler\Templates\XXLanguageXXVsPackage\obj\
%RemoveCmd% Nitra.TestsLauncher\bin\
%RemoveCmd% Nitra.TestsLauncher\obj\
%RemoveCmd% Nitra.Visualizer\bin\
%RemoveCmd% Nitra.Visualizer\obj\
%RemoveCmd% Tests\Cpp.Grammar.Test\bin\
%RemoveCmd% Tests\CSharp.Grammar.Test\bin\
%RemoveCmd% Ide\Nitra.VisualStudio\bin\
%RemoveCmd% Ide\Nitra.VisualStudio\obj\
%RemoveCmd% Ide\Nitra.VisualStudio.Plugin\bin\
%RemoveCmd% Ide\Nitra.VisualStudio.Plugin\obj\
%RemoveCmd% Ide\CSharp.VisualStudio.Plugin\bin
%RemoveCmd% Ide\CSharp.VisualStudio.Plugin\obj
%RemoveCmd% Ide\NitraCSharpVsPackage
%RemoveCmd% Ide\NitraLangVsPackage
%RemoveCmd% packages\
%RemoveCmd% VisualStudioExtensions\NitraVsPackage\packages\

MKDIR bin\Debug\NuGetSource
MKDIR bin\Release\NuGetSource

IF "%NemerleBinPathRoot%" NEQ "" GOTO CopyNemerleNuGetPkgs
SET NemerleBinPathRoot=%ProgramFiles%\Nemerle\

:CopyNemerleNuGetPkgs

copy "%NemerleBinPathRoot%Net-4.0\*.nupkg" bin\Debug\NuGetSource\ /B
copy "%NemerleBinPathRoot%Net-4.0\*.nupkg" bin\Release\NuGetSource\ /B
@echo Done