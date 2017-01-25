﻿#region Using statements

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;

using static OpenGL.OpenGL;
using static OpenGL.OpenGL.GLenum;
using static OpenGL.OpenGL.GLboolean;
using static OpenGL.GLEW;

using static SDL2.SDL;
using static SDL2.SDL_image; // Unused, outside of the deprecated LoadImage function, kept for future use
using static SDL2.SDL_ttf; // Unused, kept for future use

#endregion

namespace Sculptition
{
	static class Sculptition
	{
		#region Variable declaration

		// Settings
		static int
			screenWidth = 1024,
			screenHeight = 576;

		// IO
		static Assembly assembly;
		static StreamReader shaderReader;

		// OpenGL
		static GLuint glProgramID;

		static GLint vertexPos3DLocation = -1;
		static GLint vertexColorLocation = -1;

		static GLuint[] posVBO = new GLuint[1];
		static GLuint[] colVBO = new GLuint[1];
		static GLuint[] VAO = new GLuint[1];

		// SDL2
		static Window glWindow = new Window();

		#endregion

		static void Main()
		{
			bool errorOccurred = false;

			if (!Init())
			{
				Extensions.ConsoleWriteError("Initialization failed!");
				errorOccurred = true;
			}
			else
			{
				bool quit = false;
				SDL_Event e;
					
				SDL_RaiseWindow(glWindow.window);

				// While running
				while (!quit)
				{
					while (SDL_PollEvent(out e) != 0)
					{
						if (e.type == SDL_EventType.SDL_QUIT)
						{
							quit = true;
						}
						else if (e.type == SDL_EventType.SDL_KEYDOWN)
						{
							switch (e.key.keysym.sym)
							{
								case SDL_Keycode.SDLK_ESCAPE:
									quit = true;
									break;
							}
						}

						// Window events
						glWindow.HandleEvent(e);

						RenderQuad();

						SDL_GL_SwapWindow(glWindow.window);
					}
				}
			}

			GLenum glError = glGetError();
			if (glError != GL_NO_ERROR)
			{
				Extensions.ConsoleWriteError("OpenGL encountered an error!");
				Extensions.ConsoleWriteError("GL error: " + glError);
				errorOccurred = true;
			}

			if (errorOccurred) Console.ReadKey(); // Pause before quitting so console errors can be reviewed
			Quit();
		}

		#region Rendering functions

		/// <summary>
		/// Renders a multicolored quad to the screen.
		/// </summary>
		static void RenderQuad()
		{
			// Clear color buffer
			glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

			glUseProgram(glProgramID);

			glBindVertexArray(VAO[0]);

			glDrawArrays(GL_TRIANGLES, 0, 6);

			// Unbind program
			glUseProgram(0);
		}

		#endregion

		#region Init & quit functions

		/// <summary>
		/// Initializes SDL and the associated OpenGL context, then initializes OpenGL itself.
		/// </summary>
		/// <returns>Boolean representing execution success.</returns>
		static bool Init()
		{
			bool success = true;

			// Initialize assembly
			assembly = Assembly.GetExecutingAssembly();

			if (SDL_Init(SDL_INIT_VIDEO) != 0)
			{
				Extensions.ConsoleWriteError("SDL could not initialize! SDL error: " + SDL_GetError());
				success = false;
			}
			else
			{
				SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 4);
				SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 5);
				SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);

				SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_MULTISAMPLEBUFFERS, 1);
				SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_MULTISAMPLESAMPLES, 2);

				if (!glWindow.Init("OpenGL Test", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, screenWidth, screenHeight,
					SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL_WindowFlags.SDL_WINDOW_RESIZABLE))
				{
					Extensions.ConsoleWriteError("OpenGL window initialization failed! SDL error: " + SDL_GetError());
					success = false;
				}
				else
				{
					IntPtr glContext = SDL_GL_CreateContext(glWindow.window);

					if (glContext == IntPtr.Zero)
					{
						Extensions.ConsoleWriteError("Failed to create OpenGL context! SDL error: " + SDL_GetError());
						success = false;
					}
					else
					{
						Console.WriteLine("GLEW support for OpenGL 4.0 availability: " + glewIsSupported("GL_VERSION_4_0  GL_ARB_point_sprite"));

						// VSync
						if (SDL_GL_SetSwapInterval(1) < 0)
						{
							Extensions.ConsoleWriteWarning("Failed to set OpenGL VSync. SDL error: " + SDL_GetError());
							success = false;
						}

						if (!InitGL())
						{
							Extensions.ConsoleWriteError("Failed to initialize OpenGL!");
							success = false;
						}
					}
				}
			}

			if (success) Console.WriteLine("Initialization completed successfully");

			return success;
		}

		/// <summary>
		/// Initializes OpenGL, then creates and links shaders, shader programs, VBOs and VAOs.
		/// Writes OpenGL string information to console when finished.
		/// </summary>
		/// <returns>Boolean representing execution success.</returns>
		static bool InitGL()
		{
			// Initialize VBO, IBO and VAO
			posVBO[0] = 0;
			colVBO[0] = 0;
			VAO[0] = 0;

			bool success = true;
			LoadGLFunctions();

			glProgramID = glCreateProgram();
			glEnable(GL_DEPTH_TEST);
			glEnable(GL_MULTISAMPLE);

			// Vertex shader
			GLuint vertexShader = glCreateShader(GL_VERTEX_SHADER);

			shaderReader = new StreamReader(assembly.GetManifestResourceStream("Sculptition.vertexShader.txt"));
			string[] vertexShaderSource = shaderReader.ReadToEnd().Replace("\\n", "\n").Split(new[] { Environment.NewLine }, StringSplitOptions.None);

			glShaderSource(vertexShader, (uint)vertexShaderSource.Length, vertexShaderSource, null);

			glCompileShader(vertexShader);

			GLint vShaderCompiled = GL_FALSE;
			glGetShaderiv(vertexShader, GL_COMPILE_STATUS, out vShaderCompiled);
			if (vShaderCompiled != GL_TRUE)
			{
				Extensions.ConsoleWriteError("Failed to compile vertex shader {0}!", vertexShader);
				writeShaderLog(vertexShader);
				success = false;
			}
			else
			{
				// Vertex Shader creation successful, attach it
				glAttachShader(glProgramID, vertexShader);

				// Fragment shader
				GLuint fragmentShader = glCreateShader(GL_FRAGMENT_SHADER);

				shaderReader = new StreamReader(assembly.GetManifestResourceStream("Sculptition.fragmentShader.txt"));
				string[] fragmentShaderSource = shaderReader.ReadToEnd().Replace("\\n", "\n").Split(new[] { Environment.NewLine }, StringSplitOptions.None);

				glShaderSource(fragmentShader, (uint)fragmentShaderSource.Length, fragmentShaderSource, null);

				glCompileShader(fragmentShader);

				GLint fShaderCompiled = GL_FALSE;
				glGetShaderiv(fragmentShader, GL_COMPILE_STATUS, out fShaderCompiled);
				if (fShaderCompiled != GL_TRUE)
				{
					Extensions.ConsoleWriteError("Failed to compile fragment shader {0}!", fragmentShader);
					writeShaderLog(fragmentShader);
					success = false;
				}
				else
				{
					// Fragment Shader creation successful, attach it
					glAttachShader(glProgramID, fragmentShader);

					glLinkProgram(glProgramID);

					GLint programSuccess = GL_FALSE;
					glGetProgramiv(glProgramID, GL_LINK_STATUS, out programSuccess);
					if (programSuccess != GL_TRUE)
					{
						Extensions.ConsoleWriteError("Failed to link program {0}!", (uint)glProgramID);
						writeProgramLog(glProgramID);
						success = false;
					}
					else
					{
						// TODO: Better way of doing this; manually entering a name for every variable is dumb
						// Program linked successfully, set up shader variables
						vertexPos3DLocation = glGetAttribLocation(glProgramID, new StringBuilder("vertexPos3D"));
						vertexColorLocation = glGetAttribLocation(glProgramID, new StringBuilder("vertexColor"));
						if (vertexPos3DLocation == -1)
						{
							Extensions.ConsoleWriteError("vertexPos3D is not a valid glsl program variable!");
							success = false;
						}
						else if (vertexColorLocation == -1)
						{
							Extensions.ConsoleWriteError("vertexColor is not a valid glsl program variable!");
							success = false;
						}
						else
						{
							// Set up VBO and VAO
							glClearColor(0.8f, 0.8f, 0.8f, 1);

							GLfloat[] vertexData =
							{
								-0.5f,  0.6f,  0.0f,
								 0.5f,  0.4f,  0.0f,
								 0.5f, -0.6f,  0.0f,
								-0.5f, -0.4f,  0.0f,
								-0.5f,  0.6f,  0.0f,
								 0.5f, -0.6f,  0.0f
							};

							GLfloat[] colorData =
							{
								0.0f, 0.0f, 1.0f, // Blue
								1.0f, 1.0f, 0.0f, // Yellow
								1.0f, 0.0f, 0.0f, // Red
								0.0f, 1.0f, 0.0f, // Green
								1.0f, 0.0f, 0.0f, // Red
								0.0f, 0.0f, 1.0f  // Blue
							};

							// Create position VBO
							glGenBuffers(1, posVBO);
							glBindBuffer(GL_ARRAY_BUFFER, posVBO[0]);
							glBufferDataf(GL_ARRAY_BUFFER, 3 * 6 * sizeof(float), vertexData, GL_STATIC_DRAW);

							// Create color VBO
							glGenBuffers(1, colVBO);
							glBindBuffer(GL_ARRAY_BUFFER, colVBO[0]);
							glBufferDataf(GL_ARRAY_BUFFER, 3 * 6 * sizeof(float), colorData, GL_STATIC_DRAW);

							// Create VAO
							glGenVertexArrays(1, VAO);
							glBindVertexArray(VAO[0]);
							glEnableVertexAttribArray(vertexPos3DLocation);

							glBindBuffer(GL_ARRAY_BUFFER, posVBO[0]);
							glVertexAttribPointer(vertexPos3DLocation, 3, GL_FLOAT, GL_FALSE, 0, IntPtr.Zero);

							glBindBuffer(GL_ARRAY_BUFFER, colVBO[0]);
							glVertexAttribPointer(vertexColorLocation, 3, GL_FLOAT, GL_FALSE, 0, IntPtr.Zero);

							glEnableVertexAttribArray(vertexPos3DLocation);
							glEnableVertexAttribArray(vertexColorLocation);

							// Get OpenGL data
							Extensions.ConsoleWriteColored("\n<------OpenGL Information------>", ConsoleColor.White);

							// Renderer
							Console.WriteLine("Vendor: " + Marshal.PtrToStringAnsi(glGetString(GL_VENDOR)));
							Console.WriteLine("Renderer: " + Marshal.PtrToStringAnsi(glGetString(GL_RENDERER)));
							Console.WriteLine("Version: " + Marshal.PtrToStringAnsi(glGetString(GL_VERSION)));
							Console.WriteLine("Shading Language Version: " + Marshal.PtrToStringAnsi(glGetString(GL_SHADING_LANGUAGE_VERSION)));

							Extensions.ConsoleWriteColored("<------OpenGL Information------>\n", ConsoleColor.White);
						}
					}
				}
			}

			Console.WriteLine("OpenGL initialization finished. glGetError: " + glGetError());

			return success;
		}

		/// <summary>
		/// Frees SDL windows and calls SDL & SDL extension quit functions.
		/// </summary>
		static void Quit()
		{
			glWindow.Free();

			// Commented out because these are not initialized anymore
			//TTF_Quit();
			//IMG_Quit();
			SDL_Quit();
		}

		#endregion

		#region Debugging & logging functions

		/// <summary>
		/// Writes given program info log to the console.
		/// </summary>
		/// <param name="program">The program to write the info log of.</param>
		static void writeProgramLog(GLuint program)
		{
			if (glIsProgram(program) == GL_TRUE)
			{
				GLuint infoLogLength = 0;
				GLint maxLength = 0;

				glGetProgramiv(program, GL_INFO_LOG_LENGTH, out maxLength);
				
				StringBuilder infoLog = new StringBuilder(maxLength);

				// Untested; if this doesn't work, change infoLogLength to a uint
				glGetProgramInfoLog(program, (uint)(int)maxLength, out infoLogLength, infoLog);
				if (infoLogLength > 0)
				{
					Console.WriteLine(infoLog);
				}
			}
			else
			{
				Extensions.ConsoleWriteError("GLuint {0} is not a program!", (uint)program);
			}
		}

		/// <summary>
		/// Writes given shader info log to the console.
		/// </summary>
		/// <param name="shader">Shader to write the info log of.</param>
		static void writeShaderLog(GLuint shader)
		{
			if (glIsShader(shader) == GL_TRUE)
			{
				GLuint infoLogLength = 0;
				GLint maxLength = 0;

				glGetShaderiv(shader, GL_INFO_LOG_LENGTH, out maxLength);

				StringBuilder infoLog = new StringBuilder(maxLength);

				glGetShaderInfoLog(shader, (uint)(int)maxLength, out infoLogLength, infoLog);
				if (infoLogLength > 0)
				{
					Console.WriteLine(infoLog);
				}
			}
			else
			{
				Extensions.ConsoleWriteError("GLuint {1} is not a shader!", (uint)shader);
			}
		}

		#endregion

		#region Loading functions

		/// <summary>
		/// Load an image from a given string path, then converts it for use with a SDL renderer.
		/// </summary>
		/// <param name="path">Path to image to load. Accepts a PNG or BMP.</param>
		/// <returns></returns>
		[Obsolete("Carried over from pre-OpenGL and may be unusable. Call at your own risk.")]
		static IntPtr LoadImage(string path)
		{
			IntPtr convertedTexture = IntPtr.Zero;
			IntPtr loadedSurface = IMG_Load(path);
			
			if (loadedSurface == IntPtr.Zero)
			{
				Extensions.ConsoleWriteError("Failed to load image at path '{0}'. SDL Error: {1}", path, SDL_GetError());
			}
			else
			{
				// Convert to texture
				convertedTexture = SDL_CreateTextureFromSurface(glWindow.renderer, loadedSurface);
				if (convertedTexture == IntPtr.Zero)
				{
					Extensions.ConsoleWriteError("Failed to convert image from path '{0}'. SDL Error: {1}", path, SDL_GetError());
				}

				SDL_FreeSurface(loadedSurface);
			}

			return convertedTexture;
		}

		#endregion

		#region Other functions

		/// <summary>
		/// Create a new SDL_Rect from given values.
		/// </summary>
		/// <param name="x">X coordinate.</param>
		/// <param name="y">Y coordinate.</param>
		/// <param name="w">Width.</param>
		/// <param name="h">Height.</param>
		/// <returns>Newly created SDL_Rect.</returns>
		public static SDL_Rect createRect(int x, int y, int w, int h)
		{
			SDL_Rect newRect;
			newRect.x = x;
			newRect.y = y;
			newRect.w = w;
			newRect.h = h;

			return newRect;
		}

		#endregion
	}

	public static class Extensions
	{
		/// <summary>
		/// Allocates memory for and then converts given SDL_Rect to an IntPtr.
		/// Be sure to keep a reference to this and to free the used up memory when done.
		/// </summary>
		/// <param name="rect">The rect to be converted.</param>
		/// <param name="fDeleteOld">Passed to Marshal.StructureToPtr.</param>
		/// <returns>IntPtr representing the provided rect.</returns>
		public static IntPtr getRaw(this SDL_Rect rect, bool fDeleteOld = true)
		{
			IntPtr newRectRaw = Marshal.AllocHGlobal(Marshal.SizeOf(rect));
			Marshal.StructureToPtr(rect, newRectRaw, fDeleteOld);

			return newRectRaw;
		}

		/// <summary>
		/// Populate an array with objects.
		/// </summary>
		/// <typeparam name="T">Array object type.</typeparam>
		/// <param name="array">Array to populate</param>
		/// <param name="provider">Object to populate with.</param>
		/// <returns>The populated array.</returns>
		public static T[] Populate<T>(this T[] array, Func<T> provider)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = provider();
			}

			return array;
		}

		/// <summary>
		/// Write an error message to the console via WriteLine.
		/// </summary>
		/// <param name="message">The message to display.</param>
		/// <param name="arg0">First argument.</param>
		/// <param name="arg1">Second argument.</param>
		/// <param name="arg2">Third argument.</param>
		/// <param name="arg3">Fourth argument.</param>
		public static void ConsoleWriteError(string message, object arg0 = null, object arg1 = null, object arg2 = null, object arg3 = null)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(message, arg0, arg1, arg2, arg3);
			Console.ResetColor();
		}

		/// <summary>
		/// Write an warning message to the console via WriteLine.
		/// </summary>
		/// <param name="message">The message to display.</param>
		/// <param name="arg0">First argument.</param>
		/// <param name="arg1">Second argument.</param>
		/// <param name="arg2">Third argument.</param>
		/// <param name="arg3">Fourth argument.</param>
		public static void ConsoleWriteWarning(string message, object arg0 = null, object arg1 = null, object arg2 = null, object arg3 = null)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(message, arg0, arg1, arg2, arg3);
			Console.ResetColor();
		}

		/// <summary>
		/// Write a colored message to the console via WriteLine.
		/// </summary>
		/// <param name="message">The message to display.</param>
		/// <param name="foregroundColor">The foreground color to use.</param>
		/// <param name="arg0">First argument.</param>
		/// <param name="arg1">Second argument.</param>
		/// <param name="arg2">Third argument.</param>
		/// <param name="arg3">Fourth argument.</param>
		public static void ConsoleWriteColored(string message, ConsoleColor foregroundColor, object arg0 = null, object arg1 = null, object arg2 = null,
			object arg3 = null)
		{
			Console.ForegroundColor = foregroundColor;
			Console.WriteLine(message, arg0, arg1, arg2, arg3);
			Console.ResetColor();
		}
	}
}
