using System;
using System.Collections;
using System.Windows.Forms;

namespace ZZip.Main.Services
{
    public class ListViewColumnSorter : IComparer
    {
        public int SortColumn { get; set; } = 0;
        public SortOrder Order { get; set; } = SortOrder.Ascending;

        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem itemA || y is not ListViewItem itemB)
                return 0;

            var vA = itemA.Tag as VirtualItem;
            var vB = itemB.Tag as VirtualItem;

            // Folders always sorted above files
            if (vA != null && vB != null)
            {
                if (vA.IsDirectory && !vB.IsDirectory)
                    return -1;
                if (!vA.IsDirectory && vB.IsDirectory)
                    return 1;
            }

            int compareResult = 0;

            // Column 0: Name
            if (SortColumn == 0)
            {
                compareResult = string.Compare(itemA.Text, itemB.Text, StringComparison.CurrentCultureIgnoreCase);
            }
            // Column 1: Original Size
            else if (SortColumn == 1 && vA != null && vB != null)
            {
                compareResult = vA.Size.CompareTo(vB.Size);
            }
            // Column 5: Date Modified
            else if (SortColumn == 5 && vA != null && vB != null)
            {
                compareResult = vA.ModificationTime.CompareTo(vB.ModificationTime);
            }
            // Other columns: text comparison
            else
            {
                string textA = itemA.SubItems.Count > SortColumn ? itemA.SubItems[SortColumn].Text : "";
                string textB = itemB.SubItems.Count > SortColumn ? itemB.SubItems[SortColumn].Text : "";

                // Attempt numeric parse if percentage or numbers
                if (textA.EndsWith("%") && textB.EndsWith("%") &&
                    double.TryParse(textA.TrimEnd('%'), out double dA) &&
                    double.TryParse(textB.TrimEnd('%'), out double dB))
                {
                    compareResult = dA.CompareTo(dB);
                }
                else
                {
                    compareResult = string.Compare(textA, textB, StringComparison.CurrentCultureIgnoreCase);
                }
            }

            if (Order == SortOrder.Descending)
                return -compareResult;
            if (Order == SortOrder.Ascending)
                return compareResult;

            return 0;
        }
    }
}
