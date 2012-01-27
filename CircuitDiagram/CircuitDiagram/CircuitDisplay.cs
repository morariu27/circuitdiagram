﻿// CircuitDisplay.cs
//
// Circuit Diagram http://www.circuit-diagram.org/
//
// Copyright (C) 2012  Sam Fisher
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using CircuitDiagram;
using System.Windows.Media;
using CircuitDiagram.Components;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CircuitDiagram
{
    public class CircuitDisplay : FrameworkElement
    {
        public event SelectionChangedEventHandler SelectionChanged
        {
            add { AddHandler(Selector.SelectionChangedEvent, value); }
            remove { RemoveHandler(Selector.SelectionChangedEvent, value); }
        }

        public string NewComponentData { get; set; }
        public UndoManager UndoManager { get; set; }

        private Dictionary<Component, string> m_undoManagerBeforeData;

        private DrawingVisual m_backgroundVisual;
        private DrawingVisual m_selectedVisual;
        private DrawingVisual m_connectionsVisual;
        private DrawingVisual m_resizeVisual;
        private Component m_tempComponent;
        private Component m_resizingComponent;
        private CircuitDocument m_document;
        Point ComponentInternalMousePos;
        List<Component> m_selectedComponents { get; set; }
        bool m_placingComponent = false;
        Point m_mouseDownPos;

        public bool ShowConnectionPoints { get; set; }

        public CircuitDocument Document
        {
            get { return m_document; }
            set
            {
                if (Document != null)
                    foreach (CircuitDiagram.Elements.ICircuitElement element in Document.Elements)
                    {
                        RemoveLogicalChild(element.Visual);
                        RemoveVisualChild(element.Visual);
                    }
                m_document = value;
                DocumentChanged();
                m_document.Elements.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(Components_CollectionChanged);
            }
        }

        void Components_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (Component item in e.NewItems)
                {
                    AddVisualChild(item);
                    AddLogicalChild(item);
                    item.UpdateVisual();
                    item.Updated += new EventHandler(Component_Updated);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (Component item in e.OldItems)
                {
                    RemoveVisualChild(item);
                    RemoveLogicalChild(item);
                    item.Updated -= new EventHandler(Component_Updated);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                DocumentChanged();
        }

        void Component_Updated(object sender, EventArgs e)
        {
            if (m_selectedComponents.Count > 0 && sender == m_selectedComponents[0])
            {
                if (m_selectedComponents[0].ContentBounds != Rect.Empty)
                {
                    using (DrawingContext dc = m_selectedVisual.RenderOpen())
                    {
                        /*Pen stroke = new Pen(Brushes.Gray, 2d);
                        stroke.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
                        Rect rect = VisualTreeHelper.GetContentBounds(sender as Visual);
                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 0, 0, 100)), stroke, rect);*/
                        Pen stroke = new Pen(Brushes.Gray, 1d);
                        Rect rect = VisualTreeHelper.GetContentBounds(sender as Visual);
                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 0, 0, 100)), stroke, Rect.Inflate(rect, new Size(2, 2)));
                    }
                }
                m_selectedVisual.Offset = m_selectedComponents[0].Offset;
            }
        }

        public void DocumentSizeChanged()
        {
            this.Width = Document.Size.Width;
            this.Height = Document.Size.Height;

            using (DrawingContext dc = m_backgroundVisual.RenderOpen())
            {
                GuidelineSet guidelines = new GuidelineSet();
                guidelines.GuidelinesX.Add(0d);
                guidelines.GuidelinesY.Add(0d);
                dc.PushGuidelineSet(guidelines);

                dc.DrawRectangle(Brushes.White, null, new Rect(Document.Size));

                dc.Pop();
            }

            DrawConnections();
        }

        void DocumentChanged()
        {
            if (Document == null)
                return;

            this.Width = Document.Size.Width;
            this.Height = Document.Size.Height;

            using (DrawingContext dc = m_backgroundVisual.RenderOpen())
            {
                GuidelineSet guidelines = new GuidelineSet();
                guidelines.GuidelinesX.Add(0d);
                guidelines.GuidelinesY.Add(0d);
                dc.PushGuidelineSet(guidelines);

                dc.DrawRectangle(Brushes.White, null, new Rect(Document.Size));

                dc.Pop();
            }

            foreach (CircuitDiagram.Elements.ICircuitElement element in Document.Elements)
            {
                AddVisualChild(element.Visual);
                AddLogicalChild(element.Visual);
                element.UpdateVisual();
                element.Updated += new EventHandler(Component_Updated);
            }
        }

        static CircuitDisplay()
        {
            Selector.SelectionChangedEvent.AddOwner(typeof(CircuitDisplay));
        }

        public CircuitDisplay()
        {
            m_backgroundVisual = new DrawingVisual();
            m_selectedVisual = new DrawingVisual();
            m_connectionsVisual = new DrawingVisual();
            m_resizeVisual = new DrawingVisual();
            AddVisualChild(m_backgroundVisual);
            AddLogicalChild(m_backgroundVisual);
            AddVisualChild(m_selectedVisual);
            AddLogicalChild(m_selectedVisual);
            AddVisualChild(m_connectionsVisual);
            AddLogicalChild(m_connectionsVisual);
            AddVisualChild(m_resizeVisual);
            AddLogicalChild(m_resizeVisual);
            this.SnapsToDevicePixels = true;
            m_selectedComponents = new List<Component>();
            m_undoManagerBeforeData = new Dictionary<Component, string>();
        }

        public void DeleteComponentCommand(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            UndoAction undoAction = new UndoAction(UndoCommand.DeleteComponents, "delete", m_selectedComponents.ToArray());
            UndoManager.AddAction(undoAction);

            foreach (Component component in m_selectedComponents)
            {
                Document.Elements.Remove(component);
            }
            m_selectedComponents.Clear();
            foreach (Component component in Document.Components)
                component.DisconnectConnections();
            foreach (Component component in Document.Components)
                component.ApplyConnections(Document);
            DrawConnections();
        }

        public void DeleteComponentCommand_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (m_selectedComponents.Count > 0);
        }

        bool m_selectionBox = false;
        bool m_movingMouse = false;
        ComponentResizeMode m_resizing = ComponentResizeMode.None;
        Point m_resizeComponentOriginalStartEnd;
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            Point mousePos = e.GetPosition(this);
            m_mouseDownPos = mousePos;

            Rect resizingRect1 = Rect.Empty;
            Rect resizingRect2 = Rect.Empty;
            if (m_resizingComponent != null && m_resizingComponent.Horizontal && m_resizingComponent.Description.CanResize)
            {
                // Resizing a horizontal component
                resizingRect1 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.X - 2d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d);
                resizingRect2 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.Right - 4d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d);
            }
            else if (m_resizingComponent != null && m_resizingComponent.Description.CanResize)
            {
                // Resizing a vertical component
                resizingRect1 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Y - 2d, 6d, 6d);
                resizingRect2 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Bottom - 4d, 6d, 6d);
            }

            if (NewComponentData == null && (resizingRect1.IntersectsWith(new Rect(mousePos, new Size(1, 1))) || resizingRect2.IntersectsWith(new Rect(mousePos, new Size(1, 1)))))
            {
                // Enter resizing mode
                
                m_undoManagerBeforeData[m_resizingComponent] = m_resizingComponent.SerializeToString();

                if (resizingRect1.IntersectsWith(new Rect(mousePos, new Size(1, 1))))
                {
                    if (m_resizingComponent.Horizontal)
                    {
                        m_resizing = ComponentResizeMode.Left;
                        m_resizeComponentOriginalStartEnd = new Point(m_resizingComponent.Offset.X + m_resizingComponent.Size, m_resizingComponent.Offset.Y);
                    }
                    else
                    {
                        m_resizing = ComponentResizeMode.Top;
                        m_resizeComponentOriginalStartEnd = new Point(m_resizingComponent.Offset.X, m_resizingComponent.Offset.Y + m_resizingComponent.Size);
                    }
                }
                else
                {
                    if (m_resizingComponent.Horizontal)
                    {
                        m_resizing = ComponentResizeMode.Right;
                        m_resizeComponentOriginalStartEnd = new Point(m_resizingComponent.Offset.X, m_resizingComponent.Offset.Y);
                    }
                    else
                    {
                        m_resizing = ComponentResizeMode.Bottom;
                        m_resizeComponentOriginalStartEnd = new Point(m_resizingComponent.Offset.X, m_resizingComponent.Offset.Y);
                    }
                }
            }
            else if (m_selectedComponents.Count == 0)
            {
                bool foundHit = false;

                if (NewComponentData == null)
                {
                    // Check if user is selecting a component

                    VisualTreeHelper.HitTest(this, new HitTestFilterCallback(delegate(DependencyObject testObject)
                    {
                        if (testObject.GetType() == typeof(Component))
                            return HitTestFilterBehavior.ContinueSkipChildren;
                        else
                            return HitTestFilterBehavior.ContinueSkipSelf;
                    }),
                    new HitTestResultCallback(delegate(HitTestResult result)
                    {
                        if (result.VisualHit.GetType() == typeof(Component))
                        {
                            m_selectedComponents.Add(result.VisualHit as Component);
                            m_undoManagerBeforeData.Add(result.VisualHit as Component, (result.VisualHit as Component).SerializeToString());
                            m_originalOffsets.Add(result.VisualHit as Component, (result.VisualHit as Component).Offset);
                            ComponentInternalMousePos = new Point(mousePos.X - m_selectedComponents[0].Offset.X, mousePos.Y - m_selectedComponents[0].Offset.Y);

                            using (DrawingContext dc = m_selectedVisual.RenderOpen())
                            {
                                Pen stroke = new Pen(Brushes.Gray, 1d);
                                //stroke.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
                                Rect rect = VisualTreeHelper.GetContentBounds(result.VisualHit as Visual);
                                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 0, 0, 100)), stroke, Rect.Inflate(rect, new Size(2, 2)));
                            }
                            m_selectedVisual.Offset = m_selectedComponents[0].Offset;
                            m_originalSelectedVisualOffset = m_selectedVisual.Offset;

                            List<Component> removedItems = new List<Component>();
                            List<Component> addedItems = new List<Component>();
                            addedItems.Add(m_selectedComponents[0]);
                            RaiseEvent(new SelectionChangedEventArgs(Selector.SelectionChangedEvent, removedItems, addedItems));
                        }

                        foundHit = true;

                        return HitTestResultBehavior.Stop;
                    }), new PointHitTestParameters(e.GetPosition(this)));
                }

                m_movingMouse = foundHit;

                if (!foundHit)
                {
                    if (NewComponentData != null)
                    {
                        m_placingComponent = true;
                        m_tempComponent = Component.Create(NewComponentData);
                        AddVisualChild(m_tempComponent);
                        AddLogicalChild(m_tempComponent);
                    }
                    else
                    {
                        // Selection box
                        m_originalOffsets.Clear();
                        m_selectedComponents.Clear();
                        m_selectedVisual.Offset = new Vector();
                        m_selectionBox = true;
                    }
                }
            }
            else if (enclosingRect.IntersectsWith(new Rect(e.GetPosition(this), new Size(1, 1))))
            {
                m_movingMouse = true;
                m_originalOffsets.Clear();
                m_undoManagerBeforeData.Clear();
                foreach (Component component in m_selectedComponents)
                {
                    m_originalOffsets.Add(component, component.Offset);
                    m_undoManagerBeforeData.Add(component, component.SerializeToString());
                }
                m_originalSelectedVisualOffset = m_selectedVisual.Offset;
            }
            else
            {
                if (m_selectedComponents.Count > 0)
                {
                    RaiseEvent(new SelectionChangedEventArgs(Selector.SelectionChangedEvent, m_selectedComponents, new List<Component>()));

                    if (m_undoManagerBeforeData.Count > 0)
                    {
                        Dictionary<Component, string> afterData = new Dictionary<Component, string>();

                        foreach (Component component in m_selectedComponents)
                        {
                            string afterDataString = component.SerializeToString();
                            if (afterDataString == m_undoManagerBeforeData[component])
                                break;

                            afterData.Add(component, afterDataString);
                        }

                        if (afterData.Count > 0)
                        {
                            UndoAction undoAction = new UndoAction(UndoCommand.ModifyComponents, "move", m_selectedComponents.ToArray());
                            undoAction.AddData("before", m_undoManagerBeforeData);
                            undoAction.AddData("after", afterData);
                            UndoManager.AddAction(undoAction);
                            m_undoManagerBeforeData = new Dictionary<Component, string>();
                        }
                    }
                }

                m_selectedComponents.Clear();
                m_originalOffsets.Clear();
                m_undoManagerBeforeData.Clear();

                using (DrawingContext dc = m_selectedVisual.RenderOpen())
                {
                }
            }
        }

        public void DrawConnections()
        {
            using (DrawingContext dc = m_connectionsVisual.RenderOpen())
            {
                List<ConnectionCentre> connections = new List<ConnectionCentre>();
                List<Point> connectionPoints = new List<Point>();
                foreach (Component component in Document.Components)
                {
                    foreach (KeyValuePair<Point, Connection> connection in component.GetConnections())
                    {
                        if (connection.Value.IsConnected && !connections.Contains(connection.Value.Centre))
                        {
                            bool draw = false;
                            if (connection.Value.ConnectedTo.Length >= 3)
                                draw = true;
                            foreach (Connection connectedConnection in connection.Value.ConnectedTo)
                            {
                                if ((connectedConnection.Flags & ConnectionFlags.Horizontal) == ConnectionFlags.Horizontal && (connection.Value.Flags & ConnectionFlags.Vertical) == ConnectionFlags.Vertical && (connection.Value.Flags & ConnectionFlags.Edge) != (connectedConnection.Flags & ConnectionFlags.Edge))
                                    draw = true;
                                else if ((connectedConnection.Flags & ConnectionFlags.Vertical) == ConnectionFlags.Vertical && (connection.Value.Flags & ConnectionFlags.Horizontal) == ConnectionFlags.Horizontal && (connection.Value.Flags & ConnectionFlags.Edge) != (connectedConnection.Flags & ConnectionFlags.Edge))
                                    draw = true;
                                if (draw)
                                    break;
                            }

                            if (draw)
                            {
                                connections.Add(connection.Value.Centre);
                                connectionPoints.Add(new Point(connection.Key.X + component.Offset.X, connection.Key.Y + component.Offset.Y));
                            }
                        }
                        if (ShowConnectionPoints && (connection.Value.Flags & ConnectionFlags.Edge) == ConnectionFlags.Edge)
                            dc.DrawEllipse(Brushes.Blue, new Pen(Brushes.Transparent, 0d), Point.Add(connection.Key, component.Offset), 2d, 2d);
                    }
                }

                foreach (Point connectionPoint in connectionPoints)
                {
                    dc.DrawEllipse(Brushes.Black, new Pen(Brushes.Black, 1d), connectionPoint, 3d, 3d);
                }
            }
        }

        Dictionary<Component, Vector> m_originalOffsets = new Dictionary<Component, Vector>();
        Vector m_originalSelectedVisualOffset;
        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (m_resizing == ComponentResizeMode.None)
                this.Cursor = System.Windows.Input.Cursors.Arrow;

            if (m_selectedComponents.Count > 0 && m_movingMouse)
            {
                // Clear resize visual
                using (DrawingContext dc = m_resizeVisual.RenderOpen())
                {
                }

                Point mousePos = e.GetPosition(this);

                Vector offsetDelta = new Vector(mousePos.X - m_mouseDownPos.X, mousePos.Y - m_mouseDownPos.Y);

                foreach (Component component in m_selectedComponents)
                {
                    Vector newOffset = m_originalOffsets[component] + offsetDelta;

                    // Keep within bounds
                    if (newOffset.X + component.ContentBounds.Left < 0)
                        newOffset = new Vector(1 - component.ContentBounds.Left, newOffset.Y);
                    if (newOffset.Y + component.ContentBounds.Top < 0)
                        newOffset = new Vector(newOffset.X, 1 - component.ContentBounds.Top);
                    if (newOffset.X + component.ContentBounds.Right > this.Width)
                        newOffset = new Vector(this.Width - component.ContentBounds.Right, newOffset.Y);
                    if (newOffset.Y + component.ContentBounds.Bottom > this.Height)
                        newOffset = new Vector(newOffset.X, this.Height - component.ContentBounds.Bottom);

                    // Snap to grid
                    if (Math.IEEERemainder(newOffset.X, 20d) != 0)
                        newOffset.X = ComponentHelper.Snap(new Point(newOffset.X, newOffset.Y), ComponentHelper.GridSize).X;
                    if (Math.IEEERemainder(newOffset.Y, 20d) != 0)
                        newOffset.Y = ComponentHelper.Snap(new Point(newOffset.X, newOffset.Y), ComponentHelper.GridSize).Y;

                    offsetDelta = newOffset - m_originalOffsets[component];
                }

                foreach (Component component in m_selectedComponents)
                    component.Offset = m_originalOffsets[component] + offsetDelta;

                // update selection rect
                m_selectedVisual.Offset = m_originalSelectedVisualOffset + offsetDelta;

                // update connections
                foreach (Component component in Document.Components)
                    component.DisconnectConnections();
                foreach (Component component in Document.Components)
                    component.ApplyConnections(Document);
                DrawConnections();
            }
            else if (m_placingComponent)
            {
                ComponentHelper.SizeComponent(m_tempComponent, m_mouseDownPos, e.GetPosition(this));
                m_tempComponent.UpdateVisual();
            }
            else if (m_selectionBox)
            {
                m_selectedComponents.Clear();
                m_originalOffsets.Clear();

                using (DrawingContext dc = m_selectedVisual.RenderOpen())
                {
                    VisualTreeHelper.HitTest(this, new HitTestFilterCallback(delegate(DependencyObject testObject)
                    {
                        if (testObject.GetType() == typeof(Component))
                            return HitTestFilterBehavior.ContinueSkipChildren;
                        else
                            return HitTestFilterBehavior.ContinueSkipSelf;
                    }),
                new HitTestResultCallback(delegate(HitTestResult result)
                {
                    if (result.VisualHit.GetType() == typeof(Component))
                    {
                        Rect rect = VisualTreeHelper.GetContentBounds(result.VisualHit as Visual);
                        dc.PushTransform(new TranslateTransform((result.VisualHit as Component).Offset.X, (result.VisualHit as Component).Offset.Y));
                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 0, 0, 100)), null, rect);
                        dc.Pop();
                    }

                    return HitTestResultBehavior.Continue;
                }), new GeometryHitTestParameters(new RectangleGeometry(new Rect(m_mouseDownPos, e.GetPosition(this)))));

                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, 1d), new Rect(m_mouseDownPos, e.GetPosition(this)));
                }
            }
            else if (m_resizing != ComponentResizeMode.None)
            {
                Point mousePos = e.GetPosition(this);

                if (m_resizing == ComponentResizeMode.Left)
                    ComponentHelper.SizeComponent(m_resizingComponent, new Point(mousePos.X, m_resizingComponent.Offset.Y), m_resizeComponentOriginalStartEnd);
                else if (m_resizing == ComponentResizeMode.Top)
                    ComponentHelper.SizeComponent(m_resizingComponent, new Point(m_resizingComponent.Offset.X, mousePos.Y), m_resizeComponentOriginalStartEnd);
                else if (m_resizing == ComponentResizeMode.Right)
                    ComponentHelper.SizeComponent(m_resizingComponent, m_resizeComponentOriginalStartEnd, new Point(mousePos.X, m_resizingComponent.Offset.Y));
                else if (m_resizing == ComponentResizeMode.Bottom)
                    ComponentHelper.SizeComponent(m_resizingComponent, m_resizeComponentOriginalStartEnd, new Point(m_resizingComponent.Offset.X, mousePos.Y));

                m_resizeVisual.Offset = m_resizingComponent.Offset;
                using (DrawingContext dc = m_resizeVisual.RenderOpen())
                {
                    if (m_resizingComponent.Horizontal)
                    {
                        dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.X - 2d, m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d));
                        dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.Right - 4d, m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d));
                    }
                    else
                    {
                        dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.ContentBounds.Y - 2d, 6d, 6d));
                        dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.ContentBounds.Bottom - 4d, 6d, 6d));
                    }
                }

                m_resizingComponent.UpdateVisual();
                DrawConnections();
            }
            else if (m_selectedComponents.Count == 0 && NewComponentData == null)
            {
                bool foundHit = false;
                VisualTreeHelper.HitTest(this, new HitTestFilterCallback(delegate(DependencyObject testObject)
                {
                    if (testObject.GetType() == typeof(Component))
                        return HitTestFilterBehavior.ContinueSkipChildren;
                    else
                        return HitTestFilterBehavior.ContinueSkipSelf;
                }),
                new HitTestResultCallback(delegate(HitTestResult result)
                {
                    if (result.VisualHit.GetType() == typeof(Component))
                    {
                        Point mousePos = e.GetPosition(this);
                        ComponentInternalMousePos = new Point(mousePos.X - (result.VisualHit as Component).Offset.X, mousePos.Y - (result.VisualHit as Component).Offset.Y);

                        using (DrawingContext dc = m_selectedVisual.RenderOpen())
                        {
                            Pen stroke = new Pen(Brushes.Gray, 1d);
                            //stroke.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
                            Rect rect = VisualTreeHelper.GetContentBounds(result.VisualHit as Visual);
                            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 0, 0, 100)), stroke, Rect.Inflate(rect, new Size(2, 2)));
                        }
                        m_selectedVisual.Offset = (result.VisualHit as Component).Offset;
                        if ((result.VisualHit as Component).Description.CanResize)
                            m_resizingComponent = result.VisualHit as Component;
                    }

                    foundHit = true;

                    return HitTestResultBehavior.Stop;
                }), new PointHitTestParameters(e.GetPosition(this)));

                if (!foundHit)
                {
                    // Clear selection box
                    using (DrawingContext dc = m_selectedVisual.RenderOpen())
                    {
                    }
                    // Clear resize visual
                    using (DrawingContext dc = m_resizeVisual.RenderOpen())
                    {
                    }
                }
                else if (foundHit && m_resizingComponent != null)
                {
                    // If only 1 component selected, can resize

                    m_resizeVisual.Offset = m_resizingComponent.Offset;
                    using (DrawingContext dc = m_resizeVisual.RenderOpen())
                    {
                        if (m_resizingComponent.Horizontal)
                        {
                            dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.X - 2d, m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d));
                            dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.Right - 4d, m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d));
                        }
                        else
                        {
                            dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.ContentBounds.Y - 2d, 6d, 6d));
                            dc.DrawRectangle(Brushes.DarkBlue, null, new Rect(m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.ContentBounds.Bottom - 4d, 6d, 6d));
                        }
                    }

                    // Check if cursor should be changed to resizing
                    Rect resizingRect1 = Rect.Empty;
                    Rect resizingRect2 = Rect.Empty;
                    if (m_resizingComponent != null && m_resizingComponent.Horizontal && m_resizingComponent.Description.CanResize)
                    {
                        // Resizing a horizontal component
                        resizingRect1 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.X - 2d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d);
                        resizingRect2 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.Right - 4d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Top + m_resizingComponent.ContentBounds.Height / 2 - 3d, 6d, 6d);
                    }
                    else if (m_resizingComponent != null && m_resizingComponent.Description.CanResize)
                    {
                        // Resizing a vertical component
                        resizingRect1 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Y - 2d, 6d, 6d);
                        resizingRect2 = new Rect(m_resizingComponent.Offset.X + m_resizingComponent.ContentBounds.Left + m_resizingComponent.ContentBounds.Width / 2 - 3d, m_resizingComponent.Offset.Y + m_resizingComponent.ContentBounds.Bottom - 4d, 6d, 6d);
                    }

                    Rect mouseRect = new Rect(e.GetPosition(this), new Size(1,1));
                    if (resizingRect1.IntersectsWith(mouseRect))
                    {
                        if (m_resizingComponent.Horizontal)
                            this.Cursor = System.Windows.Input.Cursors.SizeWE;
                        else
                            this.Cursor = System.Windows.Input.Cursors.SizeNS;
                    }
                    else if (resizingRect2.IntersectsWith(mouseRect))
                    {
                        if (m_resizingComponent.Horizontal)
                            this.Cursor = System.Windows.Input.Cursors.SizeWE;
                        else
                            this.Cursor = System.Windows.Input.Cursors.SizeNS;
                    }
                }
            }
        }

        Rect enclosingRect = Rect.Empty;
        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            m_movingMouse = false;
            if (m_resizing != ComponentResizeMode.None)
            {
                m_resizingComponent.ResetConnections();
                m_resizingComponent.ApplyConnections(Document);
                DrawConnections();

                UndoAction undoAction = new UndoAction(UndoCommand.ModifyComponents, "Move component", new Component[] { m_resizingComponent });
                undoAction.AddData("before", m_undoManagerBeforeData);
                Dictionary<Component, string> afterDictionary = new Dictionary<Component, string>(1);
                afterDictionary.Add(m_resizingComponent, m_resizingComponent.SerializeToString());
                undoAction.AddData("after", afterDictionary);
                UndoManager.AddAction(undoAction);
                m_undoManagerBeforeData = new Dictionary<Component, string>();
            }
            m_resizing = ComponentResizeMode.None;
            this.Cursor = System.Windows.Input.Cursors.Arrow;
            m_resizingComponent = null;

            if (m_placingComponent)
            {
                Component newComponent = Component.Create(NewComponentData);
                ComponentHelper.SizeComponent(newComponent, m_mouseDownPos, e.GetPosition(this));
                newComponent.UpdateVisual();
                Document.Elements.Add(newComponent);
                newComponent.ApplyConnections(Document);
                DrawConnections();
                m_placingComponent = false;

                UndoAction undoAction = new UndoAction(UndoCommand.AddComponent, "Add component", newComponent);
                UndoManager.AddAction(undoAction);

                RemoveVisualChild(m_tempComponent);
                RemoveLogicalChild(m_tempComponent);
                m_tempComponent = null;
            }
            else if (m_selectedComponents.Count > 0)
            {
                Dictionary<Component, string> afterData = new Dictionary<Component, string>();

                foreach (Component component in m_selectedComponents)
                {
                    string afterDataString = component.SerializeToString();
                    if (afterDataString == m_undoManagerBeforeData[component])
                        break;

                    afterData.Add(component, afterDataString);
                }

                if (afterData.Count > 0)
                {
                    UndoAction undoAction = new UndoAction(UndoCommand.ModifyComponents, "move", m_selectedComponents.ToArray());
                    undoAction.AddData("before", m_undoManagerBeforeData);
                    undoAction.AddData("after", afterData);
                    UndoManager.AddAction(undoAction);
                    m_undoManagerBeforeData = new Dictionary<Component, string>();
                }
            }
            else if (m_selectionBox)
            {
                using (DrawingContext dc = m_selectedVisual.RenderOpen())
                {
                    enclosingRect = Rect.Empty;

                    VisualTreeHelper.HitTest(this, new HitTestFilterCallback(delegate(DependencyObject testObject)
                    {
                        if (testObject.GetType() == typeof(Component))
                            return HitTestFilterBehavior.ContinueSkipChildren;
                        else
                            return HitTestFilterBehavior.ContinueSkipSelf;
                    }),
                    new HitTestResultCallback(delegate(HitTestResult result)
                    {
                        m_selectedComponents.Add(result.VisualHit as Component);

                        if (result.VisualHit.GetType() == typeof(Component))
                        {
                            Rect rect = VisualTreeHelper.GetContentBounds(result.VisualHit as Visual);
                            dc.PushTransform(new TranslateTransform((result.VisualHit as Component).Offset.X, (result.VisualHit as Component).Offset.Y));
                            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 0, 0, 100)), null, rect);
                            dc.Pop();

                            if (enclosingRect.IsEmpty)
                            {
                                rect.Offset((result.VisualHit as Component).Offset.X, (result.VisualHit as Component).Offset.Y);
                                enclosingRect = rect;
                            }
                            else
                            {
                                rect.Offset((result.VisualHit as Component).Offset.X, (result.VisualHit as Component).Offset.Y);
                                enclosingRect.Union(rect);
                            }
                        }

                        return HitTestResultBehavior.Continue;
                    }), new GeometryHitTestParameters(new RectangleGeometry(new Rect(m_mouseDownPos, e.GetPosition(this)))));

                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, 1d), enclosingRect);
                }

                m_selectionBox = false;
            }
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (m_resizing != ComponentResizeMode.None)
            {
                m_resizingComponent.ResetConnections();
                m_resizingComponent.ApplyConnections(Document);
                DrawConnections();

                UndoAction undoAction = new UndoAction(UndoCommand.ModifyComponents, "Move component", new Component[] { m_resizingComponent });
                undoAction.AddData("before", m_undoManagerBeforeData);
                Dictionary<Component, string> afterDictionary = new Dictionary<Component, string>(1);
                afterDictionary.Add(m_resizingComponent, m_resizingComponent.SerializeToString());
                undoAction.AddData("after", afterDictionary);
                UndoManager.AddAction(undoAction);
                m_undoManagerBeforeData = new Dictionary<Component, string>();

                m_resizing = ComponentResizeMode.None;
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }

            m_selectionBox = false;
            m_placingComponent = false;
        }

        protected override int VisualChildrenCount
        {
            get
            {
                if (Document == null)
                    return 4;
                else if (m_tempComponent != null)
                    return Document.Elements.Count + 5;
                else
                    return Document.Elements.Count + 4;
            }
        }

        protected override System.Windows.Media.Visual GetVisualChild(int index)
        {
            /* [0] => background
             * [n] => document
             * [n+1] => selectedVisual
             * [n+2] => connectionVisual
             * [n+3] => resizeVisual
             * [n+4] => tempComponent
             */

            int n = 0;
            if (Document != null)
                n = Document.Elements.Count;

            if (index == 0)
                return m_backgroundVisual;
            else if (index == n + 1)
                return m_selectedVisual;
            else if (index == n + 2)
                return m_connectionsVisual;
            else if (index == n + 3)
                return m_resizeVisual;
            else if (index == n + 4)
                return m_tempComponent;
            else
                return Document.Elements[index - 1].Visual;
        }

        /// <summary>
        /// Describes which way to resize a component.
        /// </summary>
        enum ComponentResizeMode
        {
            None,
            Left,
            Right,
            Top,
            Bottom
        }
    }
}