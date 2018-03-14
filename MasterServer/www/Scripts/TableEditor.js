/*
 * TableEditor - jQuery plugin to make editable tables.
 * Tested on jquery 1.11+.
 * Requires jquery.tablesorter and jquery.tablesorter.widgets.
 * Designed for jquery.tablesorter's blue theme.
 * Requires Dialog.js
 * MIT License - https://opensource.org/licenses/MIT
 */
/// <reference path="jquery-1.11.2.js" />
"use strict";
/*
[[ Basic Usage ]]

	Select an element with jQuery and call the TableEditor function to append a table to your element.

	The TableEditor function takes two arguments.
	
	The first argument (required) is an array of "colspec" objects, which teaches the library about the structure of the table.
	
	The second argument (optional) is the options.

	The TableEditor function returns a TableEditor object which you must use in order to put data into the table.

[[ Simple Example - Read Only table ]]

	// Assume you have the following simple data set:

	var dataSet =
		[
			{ food: "Apple", group: "fruit" },
			{ food: "Banana", group: "fruit" },
			{ food: "Carrot", group: "vegetable" }
		];

	// You wish to render this data inside a table using TableEditor.

	var myTable = $(document).TableEditor(
		[
			{ name: "Food", field: "food" },
			{ name: "Food Group", field: "group" }
		]);

	// This results in a <table> being appended to your document, with columns "Food" and "Food Group".
	// The table has no data yet, and shows a loading message.
	// To provide data to the table, call the LoadData function:

	myTable.LoadData(dataSet);

	// Now, the table contains 3 rows.

[[ Simple Example - Editable Table ]]

	// If we want our table to be editable, we must provide additional information to the TableEditor call.
	// We must tell the library the type of data contained in each column.
	// We must specify the option "readOnly" = false so that when a column is clicked, it enters edit mode.
	// Finally, we should provide callback functions for when the user wishes to delete or commit a row.  If we don't provide a callback function, the library will not create the corresponding button in the UI.

	var myTable = $(document).TableEditor(
		[
			{ name: "Food", field: "food", type: "string" },
			{ name: "Food Group", field: "group", type: "string" }
		]
		, {
			readOnly: false,
			deleteFunc: function (row)
			{
			},
			commitFunc: function (row)
			{
			}
		});

	// Add data as before, and now when clicking each row, the cells become editable and buttons appear for "Delete", "Commit", and "Cancel".

[[ Simple Example - Using a dropdown list ]]

	// Maybe we don't want people to be able to enter whatever they want in every column.
	// TableEditor supports showing a dropdown list for any column instead of a text box.
	// Lets use a dropdown box for Food Group to limit its possible values.

	[
		{ name: "Food", field: "food", type: "string" },
		{ name: "Food Group", field: "group", type: "dropdown", options: ["fruit", "vegetable"] }
	]
	
[[ Simple Example - Using a dropdown list (2) ]]

	// Maybe we don't have all possible values for the dropdown list handy at the time we initialize the table.  Instead of passing a string array as the dropdown options, we can pass a function which will be called whenever the dropdown list is created, and in this function we return the array of options.

	[
		{ name: "Food", field: "food", type: "string" },
		{ name: "Food Group", field: "group", type: "dropdown", options: getFoodGroups }}
	]

	function getFoodGroups()
	{
		return ["fruit", "vegetable"];
	}

[[ Advanced Usage ]]

	// See below
*/
(function ($)
{
	$.fn.TableEditor = function (colspecs, options)
	{
		return new TableEditor(this, colspecs, options);
	}
	function TableEditor($wrapper, colspecs, options)
	{
		var self = this;
		var tableSorter;
		this.$table = $();
		var $tbody = $();
		var data = [];
		var tblEditState =
			{
				editing: false
				, rowIdx: 0
			};

		///////////////////////////////////////////////////////////////
		// Initialization /////////////////////////////////////////////
		///////////////////////////////////////////////////////////////
		var Initialize = function ()
		{
			self.settings = $.extend(
				{
					// These are the default settings
					theme: "blue",
					readOnly: true,
					idColumn: "",
					tableId: "",
					tableClass: "",
					loadingImageUrl: "",
					deleteFunc: null,
					commitFunc: null,
					onInputResetCallback: null,
					customRowClick: null
				}, options);

			self.columnCount = colspecs.length;

			// Create table element
			var $table = self.$table = $('<table class="tablesorter tableeditor">'
				+ '<thead><tr></tr></thead>'
				+ '<tbody></tbody>'
				+ '</table>');
			if (self.settings.tableId)
				$table.attr("id", self.settings.tableId);
			if (self.settings.tableClass)
				$table.addClass(self.settings.tableClass);

			$tbody = $table.find("tbody");

			// Populate header cells.
			var $headRow = $table.find("thead tr").first();
			var headerDefs = null;
			for (var i = 0; i < colspecs.length; i++)
			{
				colspecs[i] = $.extend(
					{
						name: "" // Display name of the table column
						, field: "" // Case-sensitive name of the field
						, type: "readonly" // Determines how the field is edited.  Can be: "readonly", "string", "bool", "int", "dropdown", "custom"
						, title: "" // Tooltip text for the column header
						, defaultValue: null // The default value for the field, used when adding new rows.  When the field type is non-nullable and defaultValue is null, a suitably predictable default value shall be used (e.g. 0 for "int" types).
						, editable: true // Determines if the field is editable.
						, customRender: null // Called when it is time to render columns with type "custom". The first argument is the item/row to render. The second argument is a bool indicating if the rendered field is supposed to be editable.  The third argument is the field name.  If the editable flag is true, the function should return an html string or jquery object containing an input element.  If the editable flag is false, the function should return an html string.
						//		Example:
						//		function customRender(item, editable, fieldName)
						//		{ 
						//			return editable ? $('<input type="text" />').val(item[fieldName]) : item[fieldName];
						//		}
						, customGetValue: null // Called when it is time to read/parse the value of an editable column with type "custom". The first argument is a jquery reference to item that was created by a previous call to customRender(item, true). The function should return the value that is to be stored in the item/row object.
						//		Example:
						//		function customGetValue($input)
						//		{ 
						//			return $input.val();;
						//		}
						, sorter: null
					}, colspecs[i]);

				var col = colspecs[i];

				var $th = $('<th></th>');
				$th.text(col.name);
				if (col.title)
					$th.attr('title', col.title);
				$headRow.append($th);

				if (col.sorter)
				{
					if (!headerDefs)
						headerDefs = {};
					headerDefs[i] = { sorter: col.sorter };
				}
			}

			// Enable tablesorter / visual theme
			var widgets = ['zebra', 'stickyHeaders', 'filter'];
			var widgetOptions = { filter_placeholder: { search: 'Search', select: '' } };
			var tableSorterArgs = {
				theme: self.settings.theme,
				widgets: ['zebra', 'stickyHeaders', 'filter', 'uitheme', 'columns'],
				widgetOptions:
				{
					filter_placeholder: { search: 'Search', select: '' },
					columns: ["primary", "secondary", "tertiary"],
					filter_cssFilter: "form-control"
				}
			}
			if (headerDefs)
				tableSorterArgs.headers = headerDefs;
			if (self.settings.theme == "bootstrap")
			{
				widgets.push('uitheme');
				widgets.push('columns');
				widgetOptions.columns = ["primary", "secondary", "tertiary"];
				widgetOptions.filter_cssFilter = "form-control";
				tableSorterArgs.widthFixed = true;
				tableSorterArgs.headerTemplate = '{content} {icon}'; // Needed to add the bootstrap icon!
			}


			tableSorter = $table.tablesorter(tableSorterArgs);

			tableSorter.bind("sortEnd", SortEnded);

			// Add temporary "Loading" row.
			$tbody.append(MakeWaitingRow(self.columnCount));

			// Add the table to the document.
			$wrapper.append($table);
		}

		///////////////////////////////////////////////////////////////
		// LoadData Callback //////////////////////////////////////////
		///////////////////////////////////////////////////////////////
		this.LoadData = function (rows, isHtml)
		{
			/// <summary>Call this method to populate the table with data.</summary>  
			/// <param name="rows" type="Array">An array of objects from which the table will be populated.  Each object must have fields corresponding to the columns defined when creating this TableEditor.  Optionally, if [rows] is a string, the string will be shown in a single spanned row.</param>

			data = rows;
			if (typeof (data) == "string")
			{
				$tbody.empty();
				if (isHtml)
					$tbody.append(MakeSpannedRowHtml(self.columnCount, data));
				else
					$tbody.append(MakeSpannedRowHtml(self.columnCount, EscapeHTML(data)));
				return;
			}
			var rowBuilder = new Array("");
			for (var i = 0; i < data.length; i++)
				AppendRowMarkup(rowBuilder, data, i);
			$tbody.html(rowBuilder.join(""));

			if (!self.settings.readOnly)
				$tbody.children('tr').on('click', RowClick);
			else if (typeof self.settings.customRowClick == "function")
				$tbody.children('tr').on('click', self.settings.customRowClick);

			TableDataUpdated();
		}
		var GetRowMarkup = function (rows, index)
		{
			var rowBuilder = new Array("");
			AppendRowMarkup(rowBuilder, rows, index);
			return rowBuilder.join("");
		}
		var AppendRowMarkup = function (rowBuilder, rows, index)
		{
			var row = rows[index];
			rowBuilder.push('<tr idx="' + parseInt(index) + '"');
			if (self.settings.idColumn)
				rowBuilder.push(' pk="' + row[self.settings.idColumn] + '"');
			rowBuilder.push('>');
			AppendRowContent(rowBuilder, row);
			rowBuilder.push('</tr>');
		}
		var GetRowContent = function (row)
		{
			var rowBuilder = new Array("");
			AppendRowContent(rowBuilder, row);
			return rowBuilder.join("");
		}
		var AppendRowContent = function (rowBuilder, row)
		{
			for (var i = 0; i < colspecs.length; i++)
			{
				var col = colspecs[i];
				rowBuilder.push(GetCellMarkup(row, col.field, col.type, col.customRender));
			}
		}
		var GetCellMarkup = function (row, fieldName, fieldType, renderContentFunc)
		{
			var value;
			if (fieldType == "custom" && typeof renderContentFunc == "function")
				value = renderContentFunc(row, false, fieldName);
			else
			{
				value = row[fieldName];
				if (typeof value == "undefined")
					ConsoleLog('fieldName "' + fieldName + '" does not exist in row');
				if (fieldType == "bool")
					value = '<input type="checkbox"' + (value ? ' checked="checked"' : '') + ' disabled="disabled" />';
				else
					value = EscapeHTML(DeNullify(value))
			}
			return '<td class="cell-' + fieldName + '" cellFieldName="' + fieldName + '">' + value + '</td>';
		}
		///////////////////////////////////////////////////////////////
		// Row Editing ////////////////////////////////////////////////
		///////////////////////////////////////////////////////////////
		var RowClick = function (e)
		{
			if (tblEditState.editing)
				return;
			var $tr = $(this);
			tblEditState.rowIdx = parseInt($tr.attr("idx"));
			tblEditState.editing = true;
			var $cells = $tr.children("td");
			$cells.each(function (idx, ele)
			{
				var $cell = $(ele);
				var fieldName = $cell.attr("cellFieldName");
				var col = GetColspecFromFieldName(fieldName);
				var row = data[tblEditState.rowIdx];
				var $input = MakeInputForColSpec(col, row);
				if ($input != null)
				{
					$cell.empty();
					$cell.append($input);
				}
			});
			$tr.addClass("editing");
			var $buttonRow = $('<tr id="editButtonRow"></tr>');
			var $buttonCell = $('<td colspan="' + $cells.length + '"></td>')
			if (typeof self.settings.deleteFunc == "function")
				$buttonCell.append(MakeButton("Delete", RowDelete_Click, "red"));
			if (typeof self.settings.commitFunc == "function")
				$buttonCell.append(MakeButton("Commit", RowCommit_Click, "green"));
			$buttonCell.append(MakeButton("Cancel", RowCancel_Click));
			$buttonRow.append($buttonCell);
			$tr.after($buttonRow);
			SetEditButtonRowColor();
		}

		var RowDelete_Click = function ()
		{
			SimpleDialog.ConfirmText("Are you sure you want to delete this row?", function ()
			{
				ClearEditState(true);
				TableDataUpdated();
				self.settings.deleteFunc(data[tblEditState.rowIdx]);
				// Do not delete the row from the data array, so we won't break stored index values.
			});
		}
		var RowCommit_Click = function ()
		{
			var originalRow = $.extend({}, data[tblEditState.rowIdx]);
			UpdateCachedDataWithInputs(data[tblEditState.rowIdx], $tbody.children('tr.editing').first());
			ClearEditState(true);
			TableDataUpdated();
			self.settings.commitFunc(data[tblEditState.rowIdx], originalRow);
		}
		var RowCancel_Click = function ()
		{
			ClearEditState();
		}
		var UpdateCachedDataWithInputs = function (row, $parent)
		{
			for (var i = 0; i < colspecs.length; i++)
			{
				var col = colspecs[i];
				if (col.editable)
				{
					var $input = $parent.find('[fieldName="' + col.field + '"]').first();
					switch (col.type)
					{
						case "string":
							row[col.field] = $input.val();
							break;
						case "bool":
							row[col.field] = $input.is(":checked");
							break;
						case "int":
							row[col.field] = parseInt($input.val());
							break;
						case "dropdown":
							row[col.field] = $input.val();
							break;
						case "custom":
							if (typeof col.customGetValue == "function")
								row[col.field] = col.customGetValue($input);
							break;
						case "readonly":
						default:
							break;
					}
				}
			}
		}
		var ClearEditState = function (useRowUpdatingState)
		{
			tblEditState.editing = false;
			if (useRowUpdatingState)
			{
				// Replace with a new copy of the row in the "updating" state
				// (no edit-on-click, different background, tooltip)
				$("#editButtonRow").prev().replaceWith(MakeUpdatingRow(tblEditState.rowIdx));
			}
			else
			{
				$("#editButtonRow").prev().removeClass("editing").html(GetRowContent(data[tblEditState.rowIdx]));
			}
			$("#editButtonRow").remove();
		}
		var MakeUpdatingRow = function (idx)
		{
			var $row = $(GetRowMarkup(data, idx));
			$row.addClass('updating');
			$row.attr("title", "This row is currently being updated ...");
			return $row;
		}
		var MakeInputForColSpec = function (col, row)
		{
			var $input = null;
			if (!col.editable)
				return $input;
			switch (col.type)
			{
				case "string":
					$input = $('<input type="text" />');
					$input.val(row[col.field]);
					break;
				case "bool":
					$input = $('<input type="checkbox" />');
					if (row[col.field])
						$input.prop("checked", true);
					break;
				case "int":
					$input = $('<input type="number" step="1" />');
					$input.val(row[col.field]);
					break;
				case "dropdown":
					var dropdownOptions = col.options;
					if (typeof dropdownOptions == "function")
						dropdownOptions = dropdownOptions();
					else if (!dropdownOptions)
						dropdownOptions = [];
					$input = MakeDropdownBox(dropdownOptions, row[col.field]);
					break;
				case "custom":
					if (typeof col.customRender == "function")
						$input = $(col.customRender(row, true, col.field));
					break;
				case "readonly":
				default:
					break;
			}
			if ($input != null)
				$input.attr("fieldName", col.field);
			return $input;
		}
		var SortEnded = function ()
		{
			ReAttachRowEditButtons();
		}
		var ReAttachRowEditButtons = function ()
		{
			if (!tblEditState.editing)
				return;
			var $trEditing = $tbody.children("tr.editing").first();
			$trEditing.after($("#editButtonRow"));
			SetEditButtonRowColor();
		}
		var SetEditButtonRowColor = function ()
		{
			$("#editButtonRow td").css("background-color", $("#editButtonRow").prev().find("td").first().css("background-color"));
		}
		///////////////////////////////////////////////////////////////
		// Adding Rows ////////////////////////////////////////////////
		///////////////////////////////////////////////////////////////
		this.MakeAddForm = function (row)
		{
			/// <summary>Returns a jQuery reference to a div containing inputs for adding a row to the table, which you can append to the document.  If the settings specify the table is read only, or if no commitFunc has been provided, an empty jQuery reference is returned instead.  It is safe to call this method multiple times.</summary>  
			if (self.settings.readOnly || typeof self.settings.commitFunc != "function")
				return $();
			var $addForm = $('<div class="tableEditorAddRowForm"></div>');
			if (!row)
				row = MakeNewRowWithDefaultValues();
			for (var i = 0; i < colspecs.length; i++)
			{
				var col = colspecs[i];
				var $input = MakeInputForColSpec(col, row);
				if ($input == null)
					continue;
				$input.addClass("inputEle");
				var $item = $('<div class="inputRow"></div>');
				var $inputLabel = $('<div class="inputLabel"></div>').text(col.name);
				if (col.title)
					$inputLabel.attr('title', col.title);
				$item.append($inputLabel);
				$item.append($input);
				$addForm.append($item);
			}
			var $item = $('<div class="inputRow addFormButtons"></div>');

			var $addButton = $('<input type="button" value="Add" />');
			$addButton.click(function () { AddItemFromForm($addForm); });
			$item.append($addButton);

			var $resetButton = $('<input type="button" value="Reset" title="Reset inputs to default values" />');
			$resetButton.click(function () { ResetAddItemForm($addForm); });
			$item.append($resetButton);

			$addForm.append($item);
			return $addForm;
		}
		this.GetRowObjectFromAddForm = function ($addForm)
		{
			var newRow = MakeNewRowWithDefaultValues();
			UpdateCachedDataWithInputs(newRow, $addForm);
			return newRow;
		}
		var MakeNewRowWithDefaultValues = function ()
		{
			var row = {};
			for (var i = 0; i < colspecs.length; i++)
			{
				var col = colspecs[i];
				var defVal = col.defaultValue;
				// Handle special-case default values here.
				if (col.type == "int")
				{
					defVal = parseInt(defVal);
					if (isNaN(defVal))
						defVal = 0;
				}
				row[col.field] = defVal;
			}
			return row;
		}
		var AddItemFromForm = function ($addForm)
		{
			var newRow = MakeNewRowWithDefaultValues();
			UpdateCachedDataWithInputs(newRow, $addForm);
			data.push(newRow);

			$tbody.append(MakeUpdatingRow(data.length - 1));

			self.settings.commitFunc(newRow);
			TableDataUpdated();
		}
		var ResetAddItemForm = function ($addForm)
		{
			$addForm.replaceWith(self.MakeAddForm());
			if (typeof self.settings.onInputResetCallback == "function")
				self.settings.onInputResetCallback($addForm);
		}
		///////////////////////////////////////////////////////////////
		// Update Row /////////////////////////////////////////////////
		///////////////////////////////////////////////////////////////
		this.FinishRowUpdate = function (oldRow, newRow)
		{
			/// <summary>Called by external code after a row commit has finished.</summary>
			/// <param name="oldRow" type="Object">The row object that was originally passed to commitFunc.</param>
			/// <param name="newRow" type="Object">(Optional) A new row object containing updated data.</param>
			var idx = GetRowIndex(oldRow);
			if (idx == -1)
			{
				ConsoleLog("FinishRowUpdate() could not find oldRow: " + JSON.stringify(oldRow));
				return;
			}
			if (!newRow)
				newRow = oldRow;
			data[idx] = newRow;
			var $newRow = $(GetRowMarkup(data, idx));
			if (!self.settings.readOnly)
				$newRow.on('click', RowClick);
			var $oldRow = $('tr[idx="' + idx + '"]');
			$oldRow.replaceWith($newRow);
			TableDataUpdated();
		}
		var GetRowIndex = function (row)
		{
			for (var i = 0; i < data.length; i++)
			{
				if (!data[i])
					continue;
				if (row.id == data[i].id)
					return i;
			}
			return -1;
		}
		this.GetDataArray = function ()
		{
			return data;
		}
		this.DeleteRow = function (row)
		{
			/// <summary>Called by external code after a row delete has finished.</summary>
			/// <param name="oldRow" type="Object">The row object that was originally passed to deleteFunc.</param>
			var idx = GetRowIndex(row);
			if (idx == -1)
			{
				ConsoleLog("DeleteRow() could not find row: " + JSON.stringify(row));
				return;
			}
			data[idx] = null;
			var $row = $('tr[idx="' + idx + '"]');
			$row.remove();
			TableDataUpdated();
		}
		///////////////////////////////////////////////////////////////
		// Misc ///////////////////////////////////////////////////////
		///////////////////////////////////////////////////////////////
		var TableDataUpdated = function ()
		{
			var resort = false;
			self.$table.trigger("update", [resort]);
		}
		var GetColspecFromFieldName = function (fieldName)
		{
			for (var i = 0; i < colspecs.length; i++)
			{
				var col = colspecs[i];
				if (col.field == fieldName)
					return col;
			}
			return null;
		}
		var MakeDropdownBox = function (options, value)
		{
			var sb = new Array("");
			sb.push('<select>');
			for (var i = 0; i < options.length; i++)
			{
				sb.push('<option value="' + HtmlAttributeEncode(options[i]) + '">' + EscapeHTML(options[i]) + '</option>');
			}
			sb.push('</select>');
			var $input = $(sb.join(""));
			$input.val(value);
			if (!$input.val() && options.length > 0)
				$input.val(options[0]);
			return $input;
		}
		var MakeButton = function (text, onClick, cssClass)
		{
			var $btn = $('<input type="button" />').val(text).click(onClick);
			if (cssClass)
				$btn.addClass(cssClass);
			return $btn;
		}
		var MakeWaitingRow = function (colspan)
		{
			var content = self.settings.loadingImageUrl ? ('<img src="' + self.settings.loadingImageUrl + '" alt="Loading" />') : "Loading";
			return MakeSpannedRowHtml(colspan, content);
		}
		var MakeSpannedRowHtml = function (colspan, htmlContent)
		{
			return '<tr><td colspan="' + colspan + '">' + htmlContent + '</td></tr>';
		}
		var DeNullify = function (value)
		{
			return value == null ? "" : value;
		}
		var escape = document.createElement('textarea');
		var EscapeHTML = function (html)
		{
			escape.textContent = html;
			return escape.innerHTML;
		}
		var UnescapeHTML = function (html)
		{
			escape.innerHTML = html;
			return escape.textContent;
		}
		var HtmlAttributeEncode = function (str)
		{
			if (typeof str != "string")
				return "";
			var sb = new Array("");
			for (var i = 0; i < str.length; i++)
			{
				var c = str.charAt(i);
				switch (c)
				{
					case '"':
						sb.push("&quot;");
						break;
					case '\'':
						sb.push("&#39;");
						break;
					case '&':
						sb.push("&amp;");
						break;
					case '<':
						sb.push("&lt;");
						break;
					case '>':
						sb.push("&gt;");
						break;
					default:
						sb.push(c);
						break;
				}
			}
			return sb.join("");
		}
		function ConsoleLog(message)
		{
			try
			{
				console.log(message);
			}
			catch (ex) { }
		}

		Initialize();
	}
}(jQuery));