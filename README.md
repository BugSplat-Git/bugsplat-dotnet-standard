
[![alt text](https://user-images.githubusercontent.com/2646053/117152910-77ae4800-ad88-11eb-83a4-77a72a856d51.png "BugSplat logo")](https://www.bugsplat.com)

## Introduction

BugSplatDotNetStandard allows you to capture and track exceptions on all platforms that support .NET Standard 2.0. This includes, .NET Core, Univeral Windows Platform, Mono and more! Before continuing with the tutorial please make sure you have completed the following checklist:

- [Register](https://app.bugsplat.com/v2/sign-up) as a new BugSplat user.
- [Log in](https://app.bugsplat.com/auth0/login) using your email address.

You can also view the [MyUwpCrasher](https://github.com/BugSplat-Git/MyUwpCrasher) repo which contains a sample application with BugSplatDotNetStandard installed and configured to post exceptions to BugSplat.

## Configuration

Install the [BugSplatDotNetStandard](https://www.nuget.org/packages/BugSplatDotNetStandard/) NuGet package.

```ps
Install-Package BugSplatDotNetStandard
```

After you've installed the NuGet package, add a using statement for the `BugSplatDotNetStandard` namespace.

```cs
using BugSplatDotNetStandard;
```

Create a new instance of `BugSplat` providing it your database, application, and version. It's best to do this at the entry point of your application. Several defaults can be provided to BugSplat. You can provide default values for things such as description, email, key, user and file attachments.

```cs
var bugsplat = new BugSplat(database, application, version);
bugsplat.Attachments.Add(new FileInfo("/path/to/attachment.txt"));
bugsplat.Description = "the default description";
bugsplat.Email = "fred@bugsplat.com";
bugsplat.Key = "the key!";
bugsplat.User = "Fred";
```

The `Post` method can be used to send Exception objects to BugSplat.

```cs
try
{
    throw new Exception("BugSplat rocks!");
}
catch(Exception ex)
{
    await bugsplat.Post(exception);
}
```

Additionally, `Post` can be used to upload minidumps to BugSplat.

```cs
await bugsplat.Post(new FileInfo("/path/to/minidump.dmp"));
```

The default values for description, email, key and user can be overridden in the call to Post. Additional attachments can also be specified in the call to Post. Please note that the total size of the Post body and all attachments is limited to **2MB** by default.

```cs
var options = new ExceptionPostOptions()
{
    Description = "BugSplat rocks!",
    Email = "fred@bugsplat.com",
    User = "Fred",
    Key = "the key!"
};
options.AdditionalAttachments.Add(new FileInfo("/path/to/attachment2.txt"));

await bugsplat.Post(ex, options);
```

Once you've generated an error, navigate to the BugSplat [Dashboard](https://app.bugsplat.com/v2/dashboard) and ensure you have to correct database selected in the dropdown menu. You should see a new crash report under the **Recent Crashes** section:

![alt text](https://user-images.githubusercontent.com/2646053/117152986-885ebe00-ad88-11eb-8de7-87c6e52355e9.png "BugSplat dashboard")

 Click the link in the **ID** column to see details about the crash:

![alt text](https://user-images.githubusercontent.com/2646053/117152951-8137b000-ad88-11eb-8986-e49e2678da62.png "BugSplat crash details")

That’s it! Your application is now configured to post crash reports to BugSplat.

If you have any additional questions, feel free to email [support](mailto:support@bugsplat.com) or reach out via the chat in our web application.
