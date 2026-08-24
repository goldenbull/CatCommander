using System.Collections.Generic;
// Aliased because this class declares its own static member named DataFormat below;
// the alias sidesteps the C# binder resolving the qualified type name to that member.
using AvaloniaDataFormat = Avalonia.Input.DataFormat;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>
    /// Holds information about an automatic row drag/drop operation carried out
    /// by <see cref="Avalonia.Controls.TreeDataGrid.AutoDragDropRows"/>.
    /// </summary>
    public class DragInfo
    {
        /// <summary>
        /// Marks an <see cref="Avalonia.Input.IDataTransfer"/> as carrying a <see cref="DragInfo"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="Avalonia.Input.IDataTransfer"/> can only publicly carry byte[]/string/well-known
        /// payloads (the generic <c>DataFormat.FromSystemName&lt;T&gt;</c> overload that would allow an
        /// arbitrary reference type is marked <c>[PrivateApi]</c> - internal to Avalonia itself). So this
        /// format only marks "a TreeDataGrid row drag is in progress"; the actual live <see cref="DragInfo"/>
        /// reference - which must stay a real object reference for the <see cref="Source"/> identity check
        /// in <c>TreeDataGrid.CalculateAutoDragDrop</c> - travels via <see cref="Current"/> instead, out of
        /// band. Safe because AutoDragDropRows only supports one drag gesture at a time.
        /// </remarks>
        public static readonly Avalonia.Input.DataFormat<string> DataFormat =
            AvaloniaDataFormat.CreateStringApplicationFormat("TreeDataGridDragInfo");

        /// <summary>
        /// The <see cref="DragInfo"/> for the row drag gesture currently in progress, if any.
        /// </summary>
        internal static DragInfo? Current { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DragInfo"/> class.
        /// </summary>
        /// <param name="source">The source of the drag operation/</param>
        /// <param name="indexes">The indexes being dragged.</param>
        public DragInfo(ITreeDataGridSource source, IEnumerable<IndexPath> indexes)
        {
            Source = source;
            Indexes = indexes;
        }

        /// <summary>
        /// Gets the <see cref="ITreeDataGridSource"/> that rows are being dragged from.
        /// </summary>
        public ITreeDataGridSource Source { get; }

        /// <summary>
        /// Gets or sets the model indexes of the rows being dragged.
        /// </summary>
        public IEnumerable<IndexPath> Indexes { get; }
    }
}
