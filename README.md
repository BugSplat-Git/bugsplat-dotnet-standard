[![bugsplat-github-banner-basic-outline](https://user-images.githubusercontent.com/20464226/149019306-3186103c-5315-4dad-a499-4fd1df408475.png)](https://bugsplat.com)
<br/>
# <div align="center">BugSplat</div> 
### **<div align="center">Crash and error reporting built for busy developers.</div>**
<div align="center">
    <a href="https://twitter.com/BugSplatCo">
        <img alt="Follow @bugsplatco on Twitter" src="https://img.shields.io/twitter/follow/bugsplatco?label=Follow%20BugSplat&style=social">
    </a>
    <a href="https://discord.gg/K4KjjRV5ve">
        <img alt="Join BugSplat on Discord" src="https://img.shields.io/discord/664965194799251487?label=Join%20Discord&logo=Discord&style=social">
    </a>
</div>

## 👋 Introduction

BugSplatDotNetStandard allows you to capture and track exceptions on all platforms that support .NET Standard 2.0. This includes, .NET Core, Univeral Windows Platform, Mono and more! Before continuing with the tutorial please make sure you have completed the following checklist:

- [Register](https://app.bugsplat.com/v2/sign-up) as a new BugSplat user.
- [Log in](https://app.bugsplat.com/auth0/login) using your email address.

You can also view the [MyUwpCrasher](https://github.com/BugSplat-Git/MyUwpCrasher) repo which contains a sample application with BugSplatDotNetStandard installed and configured to post exceptions to BugSplat.

## 🏗 Installation

Install the [BugSplatDotNetStandard](https://www.nuget.org/packages/BugSplatDotNetStandard/) NuGet package.

```ps
Install-Package BugSplatDotNetStandard
```

## ⚙️ Configuration

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

The default values for description, email, key and user can be overridden in the call to Post. Additional attachments can also be specified in the call to Post. Please note that the total size of the Post body and all attachments is limited to **20MB** by default.

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

## ✅ Verification

Once you've generated an error, navigate to the BugSplat [Dashboard](https://app.bugsplat.com/v2/dashboard) and ensure you have to correct database selected in the dropdown menu. You should see a new crash report under the **Recent Crashes** section:

<img width="1727" alt="BugSplat Dashboard Page" src="https://user-images.githubusercontent.com/2646053/165813342-289ab25d-90fa-4110-8922-8bbdab687803.png">

 Click the link in the **ID** column to see details about the crash:

<img width="1712" alt="BugSplat Crash Page" src="https://user-images.githubusercontent.com/2646053/165813564-0d81640f-235e-4dd0-b19f-522493fd92d7.png">

That’s it! Your application is now configured to post crash reports to BugSplat.

## 👷 Support

If you have any additional questions, please email or [support](mailto:support@bugsplat.com) team, join us on [Discord](https://discord.gg/K4KjjRV5ve), or reach out via the chat in our web application.
