using System;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using Mono.Options;

class Generator
{
	public static void ShowHelp (OptionSet os)
	{
		Console.WriteLine ("Usage is: ");
		os.WriteOptionDescriptions (Console.Out);
		Console.WriteLine ("If no options are passed then this loads the various profiles in Mono");
		
		Environment.Exit (0);
	}
	
	public static void Main(string[] args)
	{
		string assembly = null;
		string output = null;
		string directory = null;
		
		OptionSet os = null;
		
		os = new OptionSet () {
			{ "h|?|help", v => ShowHelp (os) },
			{ "a=|assembly=", v => assembly = v },
			{ "d=|directory=", v => directory = v },
			{ "o=", v => output = v }
		};
		os.Parse (args);
		
		XDocument x = new XDocument ();
		XElement root = new XElement ("Root");
		x.Add (root);
		
		if (assembly != null){
			root.Add (LoadAssembly (assembly));
		} else if (directory != null) {
			root.Add (LoadProfile (Path.GetDirectoryName (directory), directory));
		} else {
			string d = Path.Combine (Path.GetDirectoryName (typeof (int).Assembly.CodeBase), "..").Substring (5);
			
			root.Add (LoadProfile (d + "/1.0", "1_0_Libs"));
			root.Add (LoadProfile (d + "/2.0", "3_5_Libs"));
			root.Add (LoadProfile (d + "/2.1", "Silverlight"));
			root.Add (LoadProfile (d + "/gtk-sharp-2.0", "Gtk# 2_0"));
		}
		
		if (output != null){
			using (var w = new XmlTextWriter (output, System.Text.Encoding.UTF8)){
				x.WriteTo (w);
			} 
		} else 
			Console.WriteLine (x);
		
	}
	
	public static XElement LoadProfile (string directory, string name)
	{
		Console.Error.WriteLine ("Loading directory {0}", directory);
		return new XElement ("Node", 
		              new XAttribute ("Name", name),
		              from d in Directory.GetFiles (directory)
		              where d.EndsWith (".dll")
		              select LoadAssembly (d));
	}
	
	public static string GetNamespace (TypeDefinition type)
	{
		if (type.Namespace == "")
			return "<root>";
		else 
			return type.Namespace;
	}
	
	public static XElement LoadAssembly (string s)
	{
		Console.Error.WriteLine ("    Loading assembly {0}", Path.GetFileName (s));
		AssemblyDefinition ad;
		
		try {
			 ad = TypeHelper.Resolver.Resolve (s);
		} catch {
			return new XElement ("Node", new XAttribute ("Name", "Failure to load"));
		}
		
		XElement xassembly = new XElement ("Node", 	new XAttribute ("Name", Path.GetFileName (s)));
		Dictionary<string,XElement> namespaces = new Dictionary<string, XElement> ();
		foreach (ModuleDefinition module in ad.Modules){			
			foreach (TypeDefinition type in module.Types){
				string ns = GetNamespace (type);

				if (namespaces.ContainsKey (ns))
					continue;
				
				XElement xnamespace = new XElement ("Node", new XAttribute ("Name", ns));
				xassembly.Add (xnamespace);
				namespaces [ns] = xnamespace;
			}
	
			foreach (TypeDefinition type in module.Types){
				XElement xtype = new XElement ("Node", new XAttribute ("Name", type.Name));
				XElement xnamespace = namespaces [GetNamespace (type)];
				
				xnamespace.Add (xtype);

				foreach (MethodDefinition method in type.Methods){
					MethodBody b = method.Body;
					
					if (b != null && method.Body.CodeSize > 0){
						XElement xmethod = new XElement ("Node", new XAttribute ("Name", method.Name));
						xmethod.Add (new XAttribute ("Size", method.Body.CodeSize));
					    
						xtype.Add (xmethod);
					}
				}
			}
		}
		return xassembly;
	}
	
}