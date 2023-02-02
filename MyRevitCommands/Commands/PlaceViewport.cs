﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace MyRevitCommands
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class PlaceViewport : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;
                var titleBlockIds = new List<ElementId>();
                var form = new System.Windows.Forms.Form();
                form.Size = new System.Drawing.Size(400, 400);
                form.AutoScroll = true;
                // Get the available title blocks in the project
                FilteredElementCollector collect = new FilteredElementCollector(doc);
                collect.OfCategory(BuiltInCategory.OST_TitleBlocks);
                var titleBlocks = collect.ToElements();

                // Create a checkbox for each title block
                int top = 20;
                foreach (var titleBlock in titleBlocks)
                {
                    //var checkBox = new System.Windows.Forms.CheckBox();
                    var radio = new System.Windows.Forms.RadioButton();
                    radio.Text = titleBlock.Name;
                    radio.AutoSize = true;
                    radio.Left = 20;
                    radio.Top = top;
                    radio.CheckedChanged += (sender, args) => {
                        var check = (System.Windows.Forms.RadioButton)sender;
                        if (check.Checked)
                        {
                            titleBlockIds.Add(titleBlock.Id);
                        }
                        else
                        {
                            titleBlockIds.Remove(titleBlock.Id);
                        }
                    };

                    form.Controls.Add(radio);
                    top += 25;
                }
                var button = new Button
                {
                    Text = "Select",
                    Left = 150,
                    Top = titleBlocks.Count * 20 + 40
                };
                button.AutoSize = true;
                form.Controls.Add(button);
                button.Click += (sender, args) =>
                {
                    form.Close();
                };
                form.ShowDialog();
                
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;

                    // Load the Excel file
                    FileInfo file = new FileInfo(filePath);
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                    using (ExcelPackage package = new ExcelPackage(file))
                    {
                        // Get the first worksheet
                        ExcelWorksheet worksheet = package.Workbook.Worksheets.First();

                        using (Transaction transaction = new Transaction(doc, "Place Viewports"))
                        {
                            transaction.Start();

                            // Read the data from the worksheet
                            int row = 1;
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string sheetNumber = worksheet.Cells[row, 1].Value.ToString();
                                string sheetName = worksheet.Cells[row, 2].Value.ToString();
                                string viewName = worksheet.Cells[row, 3].Value.ToString();

                                // Create a new sheet
                                ViewSheet sheet = ViewSheet.Create(doc, titleBlockIds[0]);
                                sheet.SheetNumber = sheetNumber;
                                sheet.Name = sheetName;

                                // Get the view to be placed on the sheet
                                FilteredElementCollector collector = new FilteredElementCollector(doc);
                                collector.OfCategory(BuiltInCategory.OST_Views);
                                collector.OfClass(typeof(Autodesk.Revit.DB.View));
                                Autodesk.Revit.DB.View view = collector.FirstOrDefault<Element>(e => e.Name.Equals(viewName)) as Autodesk.Revit.DB.View;

                                if (view != null)
                                {
                                    // Place the view on the sheet
                                    BoundingBoxUV outline = sheet.Outline;
                                    double xu = (outline.Max.U + outline.Min.U) / 2;
                                    double yu = (outline.Max.V + outline.Min.V) / 2;
                                    XYZ midpoint = new XYZ(xu, yu, 0);
                                    Viewport viewport = Viewport.Create(doc, sheet.Id, view.Id, midpoint);
                                }

                                row++;
                            }

                            transaction.Commit();
                        }
                    }

                    return Result.Succeeded;
                }
                else
                {
                    return Result.Cancelled;
                }
                
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
