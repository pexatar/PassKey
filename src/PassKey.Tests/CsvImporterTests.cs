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

    [Fact]
    public void ParseCsv_NordPassFormat_OnlyPasswordTypeImported()
    {
        // NordPass export format: 24 columns including a "type" column that distinguishes
        // password / credit_card / identity / note rows. Only the password rows should be
        // imported here; the other types must be skipped (richer import is future work).
        var csv =
            "name,url,additional_urls,username,password,note,cardholdername,cardnumber,cvc,pin,expirydate,zipcode,folder,shared_folder,full_name,phone_number,email,address1,address2,city,country,state,type,custom_fields\n" +
            "GitHub,https://github.com,,me@x.com,pw1,,,,,,,,,,,,,,,,,,password,\n" +
            "My Visa,,,,,,John Doe,4111111111111111,123,,12/30,,,,,,,,,,,,credit_card,\n" +
            "Personale,,,,,,,,,,,,,,Mario Rossi,3331234567,mario@x.com,Via X 1,,Milano,IT,MI,identity,\n" +
            "Wifi key,,,,,Pass: hunter2,,,,,,,,,,,,,,,,,note,\n" +
            "GitLab,https://gitlab.com,,me@x.com,pw2,Secondary note,,,,,,,,,,,,,,,,,password,";

        var vault = _importer.ParseCsv(csv);

        Assert.Equal(2, vault.Passwords.Count);
        Assert.Equal("GitHub", vault.Passwords[0].Title);
        Assert.Equal("pw1", vault.Passwords[0].Password);
        Assert.Equal("GitLab", vault.Passwords[1].Title);
        Assert.Equal("Secondary note", vault.Passwords[1].Notes);
    }

    [Fact]
    public void ParseCsv_NoteSingularAlias_ImportedAsNotes()
    {
        // NordPass uses "note" (singular). The legacy mapper only accepted "notes".
        var csv = "name,username,password,note\nGitHub,me,pw,My singular note";
        var vault = _importer.ParseCsv(csv);

        Assert.Single(vault.Passwords);
        Assert.Equal("My singular note", vault.Passwords[0].Notes);
    }
}
