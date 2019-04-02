
![alt text](https://s3.amazonaws.com/bugsplat-public/npm/header.png "BugSplat logo")

## Introduction

BugSplatDotNetStandard allows you to capture and track exceptions on all platforms that support .NET Standard 2.0. This includes, .NET Core, Univeral Windows Platform, Mono and more! Before continuing with the tutorial please make sure you have completed the following checklist:

- [Register](https://app.bugsplat.com/v2/sign-up) as a new BugSplat user.
- [Log in](https://app.bugsplat.com/auth0/login) using your email address.

You can also view the [MyUwpCrasher](todo) repo which contains a sample application with BugSplatDotNetStandard installed and configured to post crashes to BugSplat.

## Configuration

After you've installed the BugSplatDotNetStandard NuGet package, add a using statement for the BugSplatDotNetStandard namespace.

```cs
using BugSplatDotNetStandard;
```

Create a new instance of BugSplat providing it your application's name, version and the corresponding BugSplat database. It's best to do this at the entry point of your application:

```cs
var bugSplat = new BugSplat(appName, appVersion, database);
```

Add a new listener to your application's UnhandledException event. In the event listener, add a call to BugSplat's Post method, making sure to pass in the exception that was unhandled. It is recommended that you close your application after an UnhandledException. In the following example from our MyUwpCrasher sample, we mark the exception as handled so that we can await the call to Post and shutdown the application immediately after:

```cs
this.UnhandledException += async (sender, args) =>
{
    args.Handled = true;
    await bugSplat.Post(args.Exception);
    CoreApplication.Exit();
};
```

You can also configure BugSplat to upload log files at crash time. You can add as many files as you'd like, but the upload size limit is 2 MB for standard customers and 10 MB for Enterprize customers:

```cs
bugSplat.AttachFile(new FileInfo("/path/to/file.txt"));
```

## Testing the Integration

To test the integration, throw a new Exception outside of a try catch block:

```cs
throw new Exception("BugSplat rocks!");
```

You can also use BugSplat to manually post errors from a catch block:

```cs
try
{
    throw new Exception("BugSplat rocks!");
}
catch (Exception ex) 
{
    await bugSplat.Post(ex);
}
```

Once you've generated an error, navigate to the BugSplat [Dashboard](https://app.bugsplat.com/v2/dashboard) and ensure you have to correct database selected in the dropdown menu. You should see a new crash report under the 'Recent Crashes' section. Click the link in the ID column to see details about the crash:

```
// TODO BG image of the Dashboard page
// TODO BG image of the IndividualCrash page
```

That’s it! Your application is now configured to post crash reports to BugSplat.

If you have any additional questions, feel free to email [support](mailto:support@bugsplat.com) or reach out via the chat in our web application.