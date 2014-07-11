function UpdateFileTimestamp() {
  var filePath = Session.Property("CustomActionData");
  var fso = new ActiveXObject("Scripting.FileSystemObject");
  var fileStream = fso.OpenTextFile(filePath, /*ForWrite*/2,  /*CreateNew*/true, /*TristateFalse*/0);
  fileStream.Write("It's never too late to have a happy childhood.");
  fileStream.Close();
}
