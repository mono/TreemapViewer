using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Collections.Generic;
using System;

namespace Moonlight {

	public class Node : IComparer <Node> {
		public string Name;
		
		// The weight
		public int    Size;
		public int    Value;
	
		// Children of this node
		public List<Node> Children;
		
		// Used during layout: 
		public int    Area;
		public Rect   Rect;
		
		public int Compare (Node a, Node b)
		{
			int n = b.Size - a.Size;
			if (n != 0)
				return n;
			return string.Compare (b.Name, a.Name);
		}
		
		public Node Clone ()
		{
			return new Node (this);
		}
		
		public Node () 
		{
			Children = new List<Node> ();
		}
		
		public Node (int n)
		{
			Children = new List<Node> (n);
		}
		
		public Node (Node re)
		{
			Name = re.Name;
			Size = re.Size;
			Area = re.Area;
			Rect = re.Rect;
			
			Children = new List<Node> (re.Children.Count);
			foreach (Node rec in re.Children)
			Children.Add (rec);
				 
	}
}

	public class TreemapRenderer : Canvas {
		Node root;
		Rect region;
		string caption;
		
		Brush borderBrush = new SolidColorBrush (Colors.White);
		Brush backgroundBrush = new SolidColorBrush (Color.FromArgb (0xff, 0x4c, 0x4c, 0x4c));
		Brush transparentBrush = new SolidColorBrush (Colors.Transparent);
		
		public TreemapRenderer (Node source, Rect region, string caption)
		{
			Width = region.Width;
			Height = region.Height;
			Console.WriteLine ("Treemap for {0}", source.Children.Count);
			this.root = source.Clone ();
			this.region = region;
			this.Background = backgroundBrush;
			this.caption = caption;
			
			Sort (root);
			
			SetRegion (region);
		}
		
		public void SetRegion (Rect region)
		{
			Children.Clear ();
			
			if (caption != ""){
				int max;
				string formatted = MakeCaption (caption, out max);
				double w = region.Width * 1.60;
				double s = w / max;
					
				var text = new TextBlock () {
					FontSize = s,
					Text = formatted,
					Foreground = new SolidColorBrush (Colors.Gray)
				};
				SetTop (text, (region.Height-text.ActualHeight)/2);
				SetLeft (text, (region.Width-text.ActualWidth)/2);
				Children.Add (text);
			}
			
			var border = new Rectangle () { 
				Width = region.Width, 
				Height = region.Height, 
				Stroke = borderBrush,
				Opacity = 1.0,
				StrokeThickness = 1
			};
			
			Canvas.SetLeft (border, region.X);
			Canvas.SetTop (border, region.Y);
			Children.Add (border);
					
			Rect emptyArea = region;
			Squarify (emptyArea, root.Children);
			
			Plot (root.Children);
		}
	
		const int PADX = 5;
		const int PADY = 3;
		
		void Plot (List<Node> children)
		{
			foreach (Node child in children){
				Canvas host = new Canvas ();
				
				host.Width = child.Rect.Width;
				host.Height = child.Rect.Height;
				SetLeft (host, child.Rect.X);
				SetTop (host, child.Rect.Y);
				host.Background = transparentBrush;
				                                                       
				Children.Add (host);	
				
				// Create box + other stuff
				
				var rect = new System.Windows.Shapes.Rectangle ();
				rect.Width = child.Rect.Width;
				rect.Height = child.Rect.Height;			
				rect.Stroke = borderBrush;
				rect.Opacity = 1.0;
				rect.StrokeThickness = 1;
						
				host.Children.Add (rect);
				
				TextBlock text = null;
				// 
				// The text
				//
				if (child.Rect.Height > PADY*2 || child.Rect.Width > PADX*2 + 20){
					int max;
					string caption = MakeCaption (child.Name, out max);
					
					// fudge
					double w = child.Rect.Width * 1.60;
					double s = w / max;
					
					text = new TextBlock () {
						FontSize = s,
						Text = caption,
						FontFamily = new FontFamily ("Candara"),
						//Clip = new RectangleGeometry () { Rect = textClip },
						Foreground = borderBrush,
						
					};
					
					if (text.ActualHeight > child.Rect.Height){
						text.FontSize = text.FontSize / 2;
					}
	
					SetTop (text, (child.Rect.Height - text.ActualHeight)/2);
					SetLeft (text, (child.Rect.Width - text.ActualWidth)/2);
					host.Children.Add (text);
				}
	
				bool inside = false;
				host.MouseEnter += delegate {
					host.Background = new SolidColorBrush (Colors.Yellow);
					if (text != null)
						text.Foreground = new SolidColorBrush (Colors.Black);
					inside = true;
				};
				
				host.MouseLeave += delegate {
					host.Background = transparentBrush;
					if (text != null)
						text.Foreground = borderBrush;
					inside = false;
				}; 
				
				//
				// Click handling
				//
				if (child.Children.Count == 0)
					continue;
				
				bool pressed = false;
				host.MouseLeftButtonDown += delegate {
					pressed = true;
				}; 
				
				Node child_copy = child;
				
				host.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) {
					bool click = pressed && inside;
					pressed = inside = false;
	
					e.Handled = true;
					
					if (click)
						Clicked (child_copy);
				};
	
			}
		}
		
		static Timeline Animate (TimeSpan time, DependencyObject target, string path, double to)
		{
			var animation = new DoubleAnimation () {
				Duration = time,
				To = to
			};
					
			Storyboard.SetTarget (animation, target);
			Storyboard.SetTargetProperty (animation, new PropertyPath (path));
			
			return animation;
		}
		
		// Render a child
		void Clicked (Node n)
		{
			Canvas c = new TreemapRenderer (n, region, n.Name);
			c.Width = region.Width;
			c.Height = region.Height;
	
			var xlate = new TranslateTransform () {
						X = n.Rect.X,
						Y = n.Rect.Y };
			
			var scale = new ScaleTransform () {
						ScaleX = n.Rect.Width / region.Width,
						ScaleY = n.Rect.Height / region.Height };
	
			c.RenderTransform = new TransformGroup { Children = { scale, xlate } };
			c.Opacity = 0.5;
			
			// Animations
			TimeSpan time = TimeSpan.FromSeconds (0.3);
			
			var s = new Storyboard () {
				Children = {
					Animate (time, xlate, "X", 0),
					Animate (time, xlate, "Y", 0),
					Animate (time, scale, "ScaleX", 1.0),
					Animate (time, scale, "ScaleY", 1.0),	
					Animate (time, c, "Opacity", 1.0),
				}};
			
			s.Begin ();
			
			c.MouseRightButtonUp += delegate (object sender, MouseButtonEventArgs e){
				e.Handled = true;
				Children.Remove (c);
			};
			Children.Add (c);
		}
	
		static string MakeCaption (string s, out int max)
		{
			string [] elements = s.Split (new char [] {'.'});
			
			max = 0;
			foreach (string el in elements)
				if (el.Length > max)
					max = el.Length;
			return string.Join ("\n", elements);
		}
		public static double GetShortestSide (Rect r)
		{
			return Math.Min (r.Width, r.Height);
		}
		
		static void Squarify (Rect emptyArea, List<Node> children)
		{
			double fullArea = 0;
			foreach (Node child in children){
				fullArea += child.Size;
			}
			
			double area = emptyArea.Width * emptyArea.Height;
			foreach (Node child in children){
				child.Area = (int) (area * child.Size / fullArea);
			}
			
			Squarify (emptyArea, children, new List<Node> (), GetShortestSide (emptyArea));
			
			foreach (Node child in children){
				if (child.Area < 9000 || child.Children.Count == 0){
					//Console.WriteLine ("Passing on this {0} {1} {2}", child.Area, child.Children, child.Children.Count);
					continue;
				}
				
				Squarify (child.Rect, child.Children);
			}
		}
		
		static void Squarify (Rect emptyArea, List<Node> children, List<Node> row, double w)
		{
			if (children.Count == 0){
				AddRowToLayout (emptyArea, row);
				return;
			}
			
			Node head = children [0];
			
			List<Node> row_plus_head = new List<Node> (row);
			row_plus_head.Add (head);
			
			double worst1 = Worst (row, w);
			double worst2 = Worst (row_plus_head, w);
			                      
			if (row.Count == 0 || worst1 > worst2){
				List<Node> children_tail = new List<Node> (children);
				children_tail.RemoveAt (0);
				Squarify (emptyArea, children_tail, row_plus_head, w);
			} else {
				emptyArea = AddRowToLayout (emptyArea, row);
				Squarify (emptyArea, children, new List<Node>(), GetShortestSide (emptyArea));
			}
		}
		
		static double Worst (List<Node> row, double sideLength)
		{
			if (row.Count == 0)
				return 0;
			
			double maxArea = 0, minArea = double.MaxValue;
			double totalArea  = 0;
			foreach (Node n in row){
				maxArea = Math.Max (maxArea, n.Area);
				minArea = Math.Min (minArea, n.Area);
				totalArea += n.Area;
			}
			
			if (minArea == double.MaxValue)
				minArea = 0;
			
			double v1 = (sideLength * sideLength * maxArea) / (totalArea * totalArea);
			double v2 = (totalArea * totalArea) / (sideLength * sideLength * minArea);
			
			return Math.Max (v1, v2);
		}
		
		static Rect AddRowToLayout (Rect emptyArea, List<Node> row)
		{
			Rect result;
			double areaUsed = 0;
			foreach (Node n in row)
				areaUsed += n.Area;
			
			if (emptyArea.Width > emptyArea.Height){
				double w = areaUsed / emptyArea.Height;
				result = new Rect (emptyArea.X + w, emptyArea.Y, Math.Max (0, emptyArea.Width - w), emptyArea.Height);
				
				double y = emptyArea.Y;
				foreach (Node n in row){
					double h = n.Area * emptyArea.Height / areaUsed;
					
					n.Rect = new Rect (emptyArea.X, y, w, h);
					//Console.WriteLine ("       PLACE Item {0}->{1}", n.Name, n.Rect);
					//Console.WriteLine ("Slot {0} with {1} got {2}", n.Name, n.Size, n.Rect);
					y += h;
				}
			} else {
				double h = areaUsed / emptyArea.Width;
				//Console.WriteLine ("   Height > Width: {0}", h);
				result = new Rect (emptyArea.X, emptyArea.Y + h, emptyArea.Width, Math.Max (0, emptyArea.Height - h));
				
				double x = emptyArea.X;
				foreach (Node n in row){
					double w = n.Area * emptyArea.Width / areaUsed;
					n.Rect = new Rect (x, emptyArea.Y, w, h);
					x += w;
				}
			}
			
			return result;
		}
		
		static void Sort (Node n)
		{
			n.Children.Sort (n);
			foreach (Node child in n.Children)
				Sort (child);
		}
	}
	
	
	
}