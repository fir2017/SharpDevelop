﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

using ICSharpCode.Reports.Core.BaseClasses;
using ICSharpCode.Reports.Core.BaseClasses.Printing;
using ICSharpCode.Reports.Core.Events;
using ICSharpCode.Reports.Core.Interfaces;
using ICSharpCode.Reports.Expressions.ReportingLanguage;

namespace ICSharpCode.Reports.Core.Exporter
{

	/// <summary>
	/// Description of BaseConverter.
	/// </summary>
	
	public class BaseConverter:IBaseConverter
	{
		
		private Size saveSize;
		private IExpressionEvaluatorFacade evaluator;
	
		public event EventHandler <NewPageEventArgs> PageFull;
		public event EventHandler<SectionRenderEventArgs> SectionRendering;
		public event EventHandler<GroupHeaderEventArgs> GroupHeaderRendering;
		public event EventHandler<GroupFooterEventArgs> GroupFooterRendering;
		public event EventHandler<RowRenderEventArgs> RowRendering;

		public BaseConverter(IDataNavigator dataNavigator,ExporterPage singlePage,
		                     ILayouter layouter)
		{
			if (dataNavigator == null) {
				throw new ArgumentNullException("dataNavigator");
			}
			if (singlePage == null) {
				throw new ArgumentNullException("singlePage");
			}

			if (layouter == null) {
				throw new ArgumentNullException("layouter");
			}
			this.SinglePage = singlePage;
			this.DataNavigator = dataNavigator;
			SectionBounds = this.SinglePage.SectionBounds;
			this.Layouter = layouter;
			this.Evaluator = StandardPrinter.CreateEvaluator(this.SinglePage,this.DataNavigator);
		}
		
		
		#region PageBreak
		
		protected void BuildNewPage(ExporterCollection myList,BaseSection section)
		{
			FirePageFull(myList);
			section.SectionOffset = SinglePage.SectionBounds.PageHeaderRectangle.Location.Y;
			myList.Clear();
		}
		
		
		protected void FirePageFull (ExporterCollection items)
		{
			EventHelper.Raise<NewPageEventArgs>(PageFull,this,new NewPageEventArgs(items));
		}
		
		
		#endregion
		
		#region Events
		
		protected void FireRowRendering (ISimpleContainer detailRow,IDataNavigator currentNavigator)
		{
			BaseRowItem row = detailRow as BaseRowItem;
			if (row == null) {
				throw new ArgumentException("row");
			}
			RowRenderEventArgs rrea = new RowRenderEventArgs(row,currentNavigator.Current);
			EventHelper.Raise<RowRenderEventArgs>(RowRendering,this,rrea);
		}
		
		
		protected void FireGroupHeaderRendering (BaseGroupedRow groupHeader)
		{
			GroupHeaderEventArgs ghea = new GroupHeaderEventArgs(groupHeader);
			EventHelper.Raise<GroupHeaderEventArgs>(GroupHeaderRendering,this,ghea);
		}
			
		
		protected void FireGroupFooterRendering (GroupFooter groupFooter)
		{
			GroupFooterEventArgs gfea = new GroupFooterEventArgs(groupFooter);
			EventHelper.Raise<GroupFooterEventArgs>(GroupFooterRendering,this,gfea);
		}
		
		
		protected void FireSectionRendering (BaseSection section)
		{
			SectionRenderEventArgs srea = new SectionRenderEventArgs(section,
			                                                         this.SinglePage.PageNumber,
			                                                         this.DataNavigator.CurrentRow,
			                                                         section);
			EventHelper.Raise<SectionRenderEventArgs>(SectionRendering,this,srea);
		}
		
		#endregion
		
		
		protected  static ExporterCollection ConvertItems (ISimpleContainer row,Point offset)		                                          
		{

			IExportColumnBuilder exportLineBuilder = row as IExportColumnBuilder;

			if (exportLineBuilder != null) {

				ExportContainer lineItem = StandardPrinter.ConvertToContainer(row,offset);
				
				StandardPrinter.AdjustBackColor(row);
				ExporterCollection list = StandardPrinter.ConvertPlainCollection(row.Items,offset);
					
				lineItem.Items.AddRange(list);
				
				ExporterCollection containerList = new ExporterCollection();
				containerList.Add (lineItem);
				return containerList;
			}
			return null;
		}
		
	
		#region Grouping
		
		protected void ConvertGroupFooter (BaseSection section,ISimpleContainer container,ExporterCollection exporterCollection)
		{
			var footers = BaseConverter.FindGroupFooter(container);
			if (footers.Count > 0) {
				FireGroupFooterRendering(footers[0]);
				Size rowSize = footers[0].Size;
				CurrentPosition = ConvertStandardRow(exporterCollection,section,(ISimpleContainer)footers[0]);
				footers[0].Size = rowSize;
			}
		}
		
		protected Point ConvertGroupChilds(ExporterCollection mylist, BaseSection section,
		                                   ISimpleContainer simpleContainer,IDataNavigator currentNavigator)
		{
			PrepareContainerForConverting(section,simpleContainer);
			FireRowRendering(simpleContainer,currentNavigator);
			Point curPos  = BaseConvert(mylist,simpleContainer,DefaultLeftPosition,CurrentPosition);
			AfterConverting (mylist);
			return curPos;
		}
		
		
		protected void PageBreakAfterGroupChange(BaseSection section,ExporterCollection exporterCollection)
		{
			
			if (CheckPageBreakAfterGroupChange(section) ) {
				
				if (DataNavigator.HasMoreData)
				{
					CurrentPosition = ForcePageBreak (exporterCollection,section);
				}
			}
		}
		
		
		private static bool CheckPageBreakAfterGroupChange(ISimpleContainer container)
		{
			var groupedRows  = BaseConverter.FindGroupHeader(container);
			if (groupedRows.Count > 0) {
				var groupedRow = groupedRows[0];
				return groupedRow.PageBreakOnGroupChange;
			}
			return false;
		}
		
		
		protected static Collection<BaseGroupedRow> FindGroupHeader (ISimpleContainer container)
		{
			return new Collection<BaseGroupedRow>(container.Items.OfType<BaseGroupedRow>().ToList());
		}
		
		
		protected static Collection<GroupFooter> FindGroupFooter (ISimpleContainer container)
		{
			return new Collection<GroupFooter>(container.Items.OfType<GroupFooter>().ToList());
		}
		
		
		protected virtual Point ForcePageBreak(ExporterCollection exporterCollection, BaseSection section)
		{
			BuildNewPage(exporterCollection,section);
			return Point.Empty;
		}
		
		#endregion
		
		#region IBaseConverter
		
		public virtual ExporterCollection Convert(BaseReportItem parent, BaseReportItem item)
		{
			this.ParentRectangle = new Rectangle(parent.Location,parent.Size);
			return new ExporterCollection();;
		}
		
		public Point CurrentPosition {get;set;}
		
		#endregion
		
		public Rectangle ParentRectangle {get;private set;}
			
		public ISinglePage SinglePage {get;private set;}
		
		public SectionBounds SectionBounds {get; private set;}
		
		
		public IDataNavigator DataNavigator {get;private set;}
			
		
		public ILayouter Layouter {get; private set;}
		
		
		public Graphics Graphics {get;set;}
		
		
		protected IExpressionEvaluatorFacade Evaluator{get;private set;}
		
		protected int DefaultLeftPosition {get;set;}
		
		
		protected void  SaveSectionSize(Size size)
		{
			this.saveSize = size;
		}
		
		
		protected Size RestoreSectionSize
		{
			get {return this.saveSize;}
		}
		
		
		protected	void PrepareContainerForConverting(BaseSection section,ISimpleContainer simpleContainer)
		{
			FireSectionRendering(section);
			LayoutRow(simpleContainer);
		}
		
		
		protected void AfterConverting (ExporterCollection convertedList)
		{
			StandardPrinter.EvaluateRow(Evaluator,convertedList);
		}
		
		
		
		protected  Point ConvertStandardRow(ExporterCollection mylist, BaseSection section, ISimpleContainer simpleContainer)
		{
			var rowSize = simpleContainer.Size;
			FillRow(simpleContainer);
			PrepareContainerForConverting(section,simpleContainer);
			Point curPos = BaseConvert(mylist,simpleContainer,DefaultLeftPosition,CurrentPosition);
			AfterConverting (mylist);
			simpleContainer.Size = rowSize;
			return curPos;
		}
		
		
		protected static Point BaseConvert(ExporterCollection myList,ISimpleContainer container,int leftPos,Point curPos)
		{
			ExporterCollection ml = BaseConverter.ConvertItems (container, curPos);		
			myList.AddRange(ml);
			return new Point (leftPos,curPos.Y + container.Size.Height + (3 *GlobalValues.GapBetweenContainer));
		}
		
		
		private void FillRow (ISimpleContainer row)
		{
			DataNavigator.Fill(row.Items);
		}
		
		
		private void LayoutRow (ISimpleContainer row)
		{
			PrintHelper.SetLayoutForRow(Graphics,Layouter,row);
		}
	}
}
