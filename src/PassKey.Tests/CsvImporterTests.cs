using PassKey.Core.Services;

namespace PassKey.Tests;

public class CsvImporterTests
{
    private readonly CsvImporter _importer = new();

    [Fact]
    public void ParseCsv_ValidData_AllFieldsMapped()
    {
        var csv = "title,username,password,url,notes\nGitHub,dev@test.com,secret123,https://github.com,My notes";
        var vault = _importer.ParseCsv(csv);

        Assert.Single(vault.Passwords);
        var pw = vault.Passwords[0];
        Assert.Equal("GitHub", pw.Title);
        Assert.Equal("dev@test.com", pw.Username);
        Assert.Equal("secret123", pw.Password);
        Assert.Equal("https://github.com", pw.Url);
        Assert.Equal("My notes", pw.Notes);
    }

    [Fact]
    public void ParseCsv_HeadersCaseInsensitive()
    {
        var csv = "Title,USERNAME,Password,URL,NOTES\nTest,user,pass,http://test.com,note";
        var vault = _importer.ParseCsv(csv);

        Assert.Single(vault.Passwords);
        Assert.Equal("Test", vault.Passwords[0].Title);
        Assert.Equal("user", vault.Passwords[0].Username);
    }

    [Fact]
    public void ParseCsv_QuotedFieldsWithCommas()
    {
        var csv = "title,username,password,notes\n\"Site, Inc.\",user,pass,\"Line1, Line2\"";
        var vault = _importer.ParseCsv(csv);

        Assert.Single(vault.Passwords);
        Assert.Equal("Site, Inc.", vault.Passwords[0].Title);
        Assert.Equal("Line1, Line2", vault.Passwords[0].Notes);
    }

    [Fact]
    public void ParseCsv_EmptyFile_ReturnsEmptyVault()
    {
        var vault = _importer.ParseCsv("");
        Assert.Empty(vault.Passwords);
    }

    [Fact]
    public void ParseCsv_HeaderOnly_ReturnsEmptyVault()
    {
        var csv = "title,username,password";
        var vault = _importer.ParseCsv(csv);
        Assert.Empty(vault.Passwords);
    }

    [Fact]
    public void ParseCsv_MissingOptionalFields_DefaultEmpty()
    {
        var csv = "title,password\nMyApp,secret";
        var vault = _importer.ParseCsv(csv);

        Assert.Single(vault.Passwords);
        Assert.Equal("MyApp", vault.Passwords[0].Title);
        Assert.Equal("secret", vault.Passwords[0].Password);
        Assert.Equal(string.Empty, vault.Passwords[0].Username);
        Assert.Equal(string.Empty, vault.Passwords[0].Url);
    }

    [Fact]
    public void ParseCsv_AlternativeHeaders_Mapped()
    {
        var csv = "name,email,pass,website,comment\nGitHub,dev@test.com,pwd,https://github.com,A note";
        var vault = _importer.ParseCsv(csv);

        Assert.Single(vault.Passwords);
        Assert.Equal("GitHub", vault.Passwords[0].Title);
        Assert.Equal("dev@test.com", vault.Passwords[0].Username);
        Assert.Equal("pwd", vault.Passwords[0].Password);
        Assert.Equal("https://github.com", vault.Passwords[0].Url);
        Assert.Equal("A note", vault.Passwords[0].Notes);
    }

    [Fact]
    public void ParseCsvLine_QuotedEscapedQuotes()
    {
        var fields = CsvImporter.ParseCsvLine("\"He said \"\"hello\"\"\",value2");
        Assert.Equal(2, fields.Count);
        Assert.Equal("He said \"hello\"", fields[0]);
        Assert.Equal("value2", fields[1]);
    }

    [Fact]
    public void ParseCsv_SkipsEmptyRows()
    {
        var csv = "title,username,password\n,,\nGitHub,user,pass";
        var vault = _importer.ParseCsv(csv);

        Assert.Single(vault.Passwords);
        Assert.Equal("GitHub", vault.Passwords[0].Title);
    }
}
