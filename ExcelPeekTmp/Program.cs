using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

var path = "c:\\Users\\Lee\\Desktop\\WC\\Schedule.xlsx";
using var doc = SpreadsheetDocument.Open(path, false);
var wbPart = doc.WorkbookPart!;
var shared = wbPart.SharedStringTablePart?.SharedStringTable;

foreach (var sheet in wbPart.Workbook.Sheets!.Elements<Sheet>())
{
	Console.WriteLine($"=== SHEET: {sheet.Name} ===");
	var wsPart = (WorksheetPart)wbPart.GetPartById(sheet.Id!);
	var rows = wsPart.Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().Take(30).ToList();

	foreach (var row in rows)
	{
		var vals = new List<string>();
		for (var c = 1; c <= 16; c++)
		{
			vals.Add(GetCellValue(wsPart, shared, row.RowIndex!, c));
		}
		Console.WriteLine($"R{row.RowIndex:00}: " + string.Join(" | ", vals));
	}
}

static string GetCellValue(WorksheetPart wsPart, SharedStringTable? shared, uint rowIndex, int colIndex)
{
	var refName = ColName(colIndex) + rowIndex;
	var cell = wsPart.Worksheet.Descendants<Cell>()
		.FirstOrDefault(c => string.Equals(c.CellReference?.Value, refName, StringComparison.OrdinalIgnoreCase));

	if (cell is null)
	{
		return string.Empty;
	}

	var text = cell.CellValue?.InnerText ?? string.Empty;
	if (cell.DataType?.Value == CellValues.SharedString && shared is not null && int.TryParse(text, out var idx))
	{
		return shared.ElementAt(idx).InnerText;
	}

	return text;
}

static string ColName(int index)
{
	var name = string.Empty;
	var n = index;
	while (n > 0)
	{
		var rem = (n - 1) % 26;
		name = (char)('A' + rem) + name;
		n = (n - 1) / 26;
	}
	return name;
}
