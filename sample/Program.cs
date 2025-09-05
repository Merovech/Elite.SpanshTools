using Elite.SpanshTools.Sample;
using Elite.SpanshTools.Sample.Helpers;

string inputFilename = HelperMethods.ValidateProgramArgs(args);
byte selection = byte.MaxValue;
DemoRunner runner = new(inputFilename);

while (selection != 0)
{
	Console.Clear();
	HelperMethods.DisplayMenu();

	// Get the user's selection, including handling invalid values
	selection = HelperMethods.GetSelection();

	// Run the selected demo.
	await runner.RunDemo((DemoMethods)selection);
}