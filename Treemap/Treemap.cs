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
	
	public class TreemapRenderer : UserControl {
		Node root;
		string caption;
		Rect region;
		
		Brush borderBrush = new SolidColorBrush (Colors.White);
		Brush backgroundBrush = new SolidColorBrush (Color.FromArgb (0xff, 0x4c, 0x4c, 0x4c));
		Brush transparentBrush = new SolidColorBrush (Colors.Transparent);
		Canvas content;
		TreemapRenderer activeChild;
		
		public TreemapRenderer (Node source, string caption)
		{

			this.root = source.Clone ();
			this.caption = caption;
			
			Sort (root);

			content = new Canvas () {
				Background = backgroundBrush
			};

			Content = content;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			base.MeasureOverride (availableSize);
			var c = Application.Current.Host.Content;
			
			return new Size(
				Math.Min(c.ActualWidth, availableSize.Width),
				Math.Min(c.ActualHeight, availableSize.Height));
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			base.ArrangeOverride (finalSize);
			content.Width = finalSize.Width;
			content.Height = finalSize.Height;
			
			Rect newRegion = new Rect (0, 0, finalSize.Width, finalSize.Height);
			if (newRegion != region && newRegion.Width > 0 && newRegion.Height > 0) {
				TreemapRenderer t = this, child;
				
				while (true){
					child = t.activeChild;
					if (child == null){
						t.SetRegion (newRegion);
						break;
					}
					t = child;
				}
			}
			
			return finalSize;
			
		}
		
		public void SetRegion (Rect newRegion)
		{
			region = newRegion;
			content.Children.Clear ();
			content.Width = region.Width;
			content.Height = region.Height;
			
			if (caption != ""){
				int max;
				string formatted = MakeCaption (caption, out max);
				double w = region.Width * 1.60;
				double s = w / max;
					
				var text = new TextBlock () {
					FontSize = s,
					Text = formatted,
					Foreground = new SolidColorBrush (Color.FromArgb (255, 0x5c, 0x5c, 0x5c))
				};
                
				Canvas.SetTop (text, (region.Height-text.ActualHeight)/2);
				Canvas.SetLeft (text, (region.Width-text.ActualWidth)/2);
				content.Children.Add (text);
			}
		
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
				Canvas.SetLeft (host, child.Rect.X);
				Canvas.SetTop (host, child.Rect.Y);
				host.Background = transparentBrush;
				                                                       
				content.Children.Add (host);	
				
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
	
					Canvas.SetTop (text, (child.Rect.Height - text.ActualHeight)/2);
					Canvas.SetLeft (text, (child.Rect.Width - text.ActualWidth)/2);
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

		Stack<TreemapRenderer> frames = new Stack<TreemapRenderer>();

		// Render a child
		void Clicked (Node n)
		{
			TreemapRenderer c = new TreemapRenderer (n, n.Name);

			Size ns = new Size(region.Width, region.Height);
			c.Measure (ns);
			c.Arrange(region);
			
			var xlate = new TranslateTransform () {
				X = n.Rect.X,
				Y = n.Rect.Y };
			
			var scale = new ScaleTransform () {
				ScaleX = n.Rect.Width / region.Width,
				ScaleY = n.Rect.Height / region.Height };
			
			c.RenderTransform = new TransformGroup { Children = { scale, xlate } };
			c.Opacity = 0.5;
			content.Children.Add (c);
			activeChild = c;
			
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
		}

		public void Back()
		{
			TreemapRenderer last = this, child = activeChild;
			
			while (child != null && child.activeChild != null) {
				last = child;
				child = child.activeChild;
			}
			if (child != null) {
				Rect childRegion = child.region;
				
				last.content.Children.Remove (child);
				last.activeChild = null;
				
				// In case layout changed while we were rendering the child
				if (childRegion != region)
					SetRegion (childRegion);
			}
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