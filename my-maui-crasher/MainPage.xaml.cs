namespace my_maui_crasher;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object sender, EventArgs e)
	{
		SampleStackFrame0();
		SemanticScreenReader.Announce("The application has crashed");
	}

	private void SampleStackFrame0()
	{
		SampleStackFrame1();
	}

	private void SampleStackFrame1()
	{
		SampleStackFrame2();
	}

	private void SampleStackFrame2()
	{
		SampleStackFrame3();
	}

	private void SampleStackFrame3()
	{
		throw new Exception("Crash");
	}
}

