///
/// MonoWSDL.cs -- a WSDL to proxy code generator.
///
/// Author: Erik LeBel (eriklebel@yahoo.ca)
///
/// Copyright (C) 2003, Erik LeBel,
///

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Services.Description;

using Microsoft.CSharp;

namespace Mono.WebServices
{
	///
	/// <summary>
	///	Document retriever.
	///
	///	By extanciating this class, and setting URL and the optional Username, Password and Domain
	///	properties, the Document property can be used to retrieve the document at the specified 
	///	address.
	///
	///	If none of Username, Password or Domain are specified the DocumentRetriever attempts
	///	to fetch the document without providing any authentication credentials. If at least one of 
	///	these values is provided, then the retrieval process is attempted with an authentication
	///	
	/// </summary>
	///	
	internal class DocumentRetriever
	{
		string url	= null;
		string domain	= null;
		string password = null;
		string username = null;
		
		
		///
		/// <summary>
		///	Set the URL from which the document will be retrieved.
		/// </summary>
		///
		public string URL
		{
			set 
			{
				if (url != null)
					throw new Exception("Too many document sources");
				
				url = value;
			}
		}
		
		///
		/// <summary>
		///	Specify the username to be used.
		/// </summary>
		///
		public string Username
		{
			set { username = value; }
		}
		
		///
		/// <summary/>
		///
		public string Password
		{
			set { password = value; }
		}
		
		///
		/// <summary/>
		///
		public string Domain
		{
			set { domain = value; }
		}
		
		///
		/// <summary>
		///	This property returns the document found at the DocumentRetriever's URL.
		/// </summary>
		///
		public string Document
		{
			get
			{
				WebClient webClient = new WebClient();
				
				if (username != null || password != null || domain != null)
				{
					NetworkCredential credentials = new NetworkCredential();
					
					if (username != null)
						credentials.UserName = username;
					
					if (password != null)
						credentials.Password = password;
					
					if (domain != null)
						credentials.Domain = domain;
					
					webClient.Credentials = credentials;
				}
	
				
				byte [] rawDocument = webClient.DownloadData(url);
				
				// FIXME this ASCII encoding is probably wrong, what about Unicode....
				string document = Encoding.ASCII.GetString(rawDocument);
				
				return document;
			}
		}
	}
	
	///
	/// <summary>
	///	Source code generator.
	/// </summary>
	///
	class SourceGenerator
	{
		string applicationSiganture 	= null;
		string appSettingURLKey		= null;
		string appSettingBaseURL	= null;
		string language			= "CS";
		string ns			= null;
		string outFilename		= null;
		string protocol			= "Soap";
		bool   server			= false;
		
		///
		/// <summary/>
		///
		public string Language 
		{
			// FIXME validate
			set { language = value; }
		}
		
		///
		/// <summary/>
		///
		public string Namespace 
		{
			set { ns = value; }
		}
		
		///
		/// <summary>
		///	The file to contain the generated code.
		/// </summary>
		///
		public string Filename
		{
			set { outFilename = value; }
		}
		
		///
		/// <summary/>
		///
		public string Protocol
		{
			// FIXME validate
			set { protocol = value; }
		}
		
		///
		/// <summary/>
		///
		public string ApplicationSignature
		{
			set { applicationSiganture = value; }
		}
		
		///
		/// <summary/>
		///
		public string AppSettingURLKey
		{
			set { appSettingURLKey = value; }
		}
		
		///
		/// <summary/>
		///
		public string AppSettingBaseURL 
		{
			set { appSettingBaseURL  = value; }
		}
		
		///
		/// <summary/>
		///
		public bool Server
		{
			set { server = value; }
		}
		
		///
		/// <summary>
		///	Generate code for the specified ServiceDescription.
		/// </summary>
		///
		public void GenerateCode(ServiceDescription serviceDescription)
		{
			// FIXME iterate over each serviceDescription.Services?
			CodeNamespace codeNamespace = GetCodeNamespace();
			CodeCompileUnit codeUnit    = new CodeCompileUnit();
			
			codeUnit.Namespaces.Add(codeNamespace);

			ServiceDescriptionImporter importer = new ServiceDescriptionImporter();
			importer.ProtocolName = protocol;
			if (server)
				importer.Style = ServiceDescriptionImportStyle.Server;
			
			importer.AddServiceDescription(serviceDescription, appSettingURLKey, appSettingBaseURL);
			
			ServiceDescriptionImportWarnings warnings = importer.Import(codeNamespace, codeUnit);
			if (warnings != 0)
				Console.WriteLine("WARNING: {0}", warnings.ToString());
			
			string serviceName = serviceDescription.Services[0].Name;
			WriteCodeUnit(codeUnit, serviceName);
		}
		
		///
		/// <summary>
		///	Create the CodeNamespace with the generator's signature commented in.
		/// </summary>
		///
		CodeNamespace GetCodeNamespace()
		{
			CodeNamespace codeNamespace = new CodeNamespace(ns);
			
			if (applicationSiganture != null)
			{
				codeNamespace.Comments.Add(new CodeCommentStatement("\n This source code was auto-generated by " + applicationSiganture + "\n"));
			}
			
			return codeNamespace;
		}
		
		///
		/// <summary/>
		///
		void WriteCodeUnit(CodeCompileUnit codeUnit, string serviceName)
		{
			CodeDomProvider 	provider 	= GetProvider();
			ICodeGenerator 		generator 	= provider.CreateGenerator();
			CodeGeneratorOptions 	options 	= new CodeGeneratorOptions();
			
			string filename;
			if (outFilename != null)
				filename = outFilename;
			else
				filename = serviceName	+ "." + provider.FileExtension;
			
			StreamWriter writer = new StreamWriter(	File.OpenWrite(filename));
			
			generator.GenerateCodeFromCompileUnit(codeUnit, writer, options);
			writer.Close();
		}
		
		///
		/// <summary>
		///	Fetch the Code Provider for the language specified by the 'language' members.
		/// </summary>
		///
		private CodeDomProvider GetProvider()
		{
			// FIXME these should be loaded dynamically using reflection
			CodeDomProvider provider;
			
			switch (language.ToUpper())
			{
			    case "CS":
				    provider = new CSharpCodeProvider();
				    break;
			    
			    default:
				    throw new Exception("Unknow language");
			}

			return provider;
		}
	}
	
	///
	/// <summary>
	///	monoWSDL's main application driver. Reads the command-line arguments and dispatch the
	///	appropriate handlers.
	/// </summary>
	///
	public class Driver
	{
		const string ProductId = "WSDL proxy generator v0.1";
		const string UsageMessage = 
			"wsdl [options] {path | URL} \n"
			+ "   -appsettingurlkey:key        (short -urlkey)\n"
			+ "   -appsettingbaseurl:baseurl   (short -baseurl)\n"
			+ "   -domain:domain (short -d)    Domain of username for server authentication\n"
			+ "   -language:language           Language of generated code. Allowed CS\n"
			+ "                                (default) (short -l)\n"
			+ "   -namespace:ns                The namespace of the generated code, default\n"
			+ "                                NS if none (short -n)\n"
			+ "   -nologo                      Surpress the startup logo\n"
			+ "   -out:filename                The target file for generated code \n"
			+ "                                (short -o)\n"
			+ "   -password:pwd                Password used to contact server (short -p)\n"
			+ "   -protocol:protocol           Protocol to implement. Allowed: Soap \n"
			+ "                                (default), HttpGet, HttpPost\n"
			+ "   -server                      Generate server instead of client proxy code.\n"
			+ "   -username:username           Username used to contact server (short -u)\n"
			+ "   -?                           Display this message\n"
			+ "\n"
			+ "Options can be of the forms  -option, --option or /option";
		
		DocumentRetriever retriever = null;
		SourceGenerator generator = null;
		
		bool noLogo = false;
		bool help = false;
		bool hasURL = false;
		
		// FIXME implement these options
		// (are they are usable by the System.Net.WebProxy class???)
		string proxy = null;
		string proxyDomain = null;
		string proxyPassword = null;
		string proxyUsername = null;

		///
		/// <summary>
		///	Initialize the document retrieval component and the source code generator.
		/// </summary>
		///
		Driver()
		{
			retriever = new DocumentRetriever();
			generator = new SourceGenerator();
			generator.ApplicationSignature = ProductId;
		}
		
		///
		/// <summary>
		///	Interperet the command-line arguments and configure the relavent components.
		/// </summary>
		///		
		void ImportArgument(string argument)
		{
			string optionValuePair;
			
			if (argument.StartsWith("--"))
			{
				optionValuePair = argument.Substring(2);
			}
			else if (argument.StartsWith("/") || argument.StartsWith("-"))
			{
				optionValuePair = argument.Substring(1);
			}
			else
			{
				hasURL = true;
				retriever.URL = argument;
				return;
			}
			
			string option;
			string value;
			
			int indexOfEquals = optionValuePair.IndexOf(':');
			if (indexOfEquals > 0)
			{
				option = optionValuePair.Substring(0, indexOfEquals);
				value = optionValuePair.Substring(indexOfEquals + 1);
			}
			else
			{
				option = optionValuePair;
				value = null;
			}
			
			switch (option)
			{
				case "appsettingurlkey":
				case "urlkey":
				    generator.AppSettingURLKey = value;
				    break;

				case "appsettingbaseurl":
				case "baseurl":
				    generator.AppSettingBaseURL = value;
				    break;

				case "d":
				case "domain":
				    retriever.Domain = value;
				    break;

				case "l":
				case "language":
				    generator.Language = value;
				    break;

				case "n":
				case "namespace":
				    generator.Namespace = value;
				    break;

				case "nologo":
				    noLogo = true;
				    break;

				case "o":
				case "out":
				    generator.Filename = value;
				    break;

				case "p":
				case "password":
				    retriever.Password = value;
				    break;

				case "protocol":
				    generator.Protocol = value;
				    break;

				case "proxy":
				    proxy = value;
				    break;

				case "proxydomain":
				case "pd":
				    proxyDomain = value;
				    break;

				case "proxypassword":
				case "pp":
				    proxyPassword = value;
				    break;

				case "proxyusername":
				case "pu":
				    proxyUsername = value;
				    break;

				case "server":
				    generator.Server = true;
				    break;

				case "u":
				case "username":
				    retriever.Username = value;
				    break;

				case "?":
				    help = true;
				    break;

				default:
				    throw new Exception("Unknown option " + option);
			}
		}

		///
		/// <summary>
		///	Driver's main control flow:
		///	 - parse arguments
		///	 - report required messages
		///	 - terminate if no input
		///	 - report errors
		/// </summary>
		///
		void Run(string[] args)
		{
			try
			{
				// parse command line arguments
				foreach (string argument in args)
				{
					ImportArgument(argument);
				}
				
				if (noLogo == false)
					Console.WriteLine(ProductId);
				
				if (help || !hasURL)
				{
					Console.WriteLine(UsageMessage);
					return;
				}
				
				// fetch the document
				StringReader reader = new StringReader(retriever.Document);
				
				// import the document as a ServiceDescription
				ServiceDescription serviceDescription = ServiceDescription.Read(reader);
				
				// generate the code
				generator.GenerateCode(serviceDescription);
			}
			catch (Exception exception)
			{
				Console.WriteLine("Error: {0}", exception.Message);
				// FIXME: surpress this except for when debug is enabled
				Console.WriteLine("Stack:\n {0}", exception.StackTrace);
			}
		}
		
		///
		/// <summary>
		///	Application entry point.
		/// </summary>
		///
		public static void Main(string[] args)
		{
			Driver d = new Driver();
			d.Run(args);
		}
	}
}
