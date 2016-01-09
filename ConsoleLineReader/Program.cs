
using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Scripting.Hosting
{
    internal class LineReader
    {
        private int _cursor;
        private bool _finished;

        private int _maxLength;
        private StringBuilder _lineText;
        
        private static Dictionary<ConsoleKey, Handler> s_keyHandlers;

        public string Prompt { set; get; }

        private struct Handler
        {
            public ConsoleKeyInfo KeyInfo;
            public ConsoleKeyHandler KeyHandler;

            public Handler(ConsoleKey key, ConsoleKeyHandler keyHandler)
            {
                KeyInfo = new ConsoleKeyInfo('\0', key, false, false, false);
                KeyHandler = keyHandler;
            }

            public Handler(ConsoleKeyInfo keyInfo, ConsoleKeyHandler keyHandler)
            {
                KeyInfo = keyInfo;
                KeyHandler = keyHandler;
            }

            public static Handler WithControl(ConsoleKey key, ConsoleKeyHandler keyHandler)
            {
                return new Handler(new ConsoleKeyInfo('\0', key, shift: false, alt: false, control: true), keyHandler);
            }
        }

        delegate void ConsoleKeyHandler();

        public LineReader()
        {
            s_keyHandlers = new Dictionary<ConsoleKey, Handler>();

            s_keyHandlers[ConsoleKey.Escape] = new Handler(ConsoleKey.Escape, Escape);
            s_keyHandlers[ConsoleKey.Home] = new Handler(ConsoleKey.Home, Home);
            s_keyHandlers[ConsoleKey.End] = new Handler(ConsoleKey.End, End);
            s_keyHandlers[ConsoleKey.LeftArrow] = new Handler(ConsoleKey.LeftArrow, LeftArrow);
            s_keyHandlers[ConsoleKey.RightArrow] = new Handler(ConsoleKey.RightArrow, RightArrow);
            s_keyHandlers[ConsoleKey.Backspace] = new Handler(ConsoleKey.Backspace, Backspace);
            s_keyHandlers[ConsoleKey.Enter] = new Handler(ConsoleKey.Enter, Enter);
        }

        private void Escape()
        {
            _lineText.Clear();
            Refresh();
            SetCursor(0);
        }

        private void Home()
        {
            SetCursor(0);
        }

        private void End()
        {
            SetCursor(_lineText.Length);
        }

        private void LeftArrow()
        {
            if (_cursor == 0)
            {
                return;
            }
            SetCursor(_cursor - 1);
        }

        private void RightArrow()
        {
            if (_cursor == _lineText.Length)
            {
                return;
            }
            SetCursor(_cursor + 1);
        }

        private void Backspace()
        {
            if (_cursor == 0)
            {
                return;
            }

            _lineText.Remove(_cursor - 1, 1);
            Refresh();
            SetCursor(_cursor - 1);
        }

        private void Enter()
        {
            _finished = true;
        }

        private void InsertChar(char insert)
        {
            _lineText.Insert(_cursor, insert);
            Refresh();
            SetCursor(_cursor + 1);
        }

        private void TypeChar(char typedChar)
        {
            if (typedChar == '\0')
            {
                return;
            }
            InsertChar(typedChar);
        }

        private void Refresh()
        {
            int len = Prompt.Length + _lineText.Length;
            int max = Math.Max(_maxLength, len);

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(Prompt);
            Console.Write(_lineText.ToString());

            // clear the rest of the line
            for (int i = len; i < _maxLength; ++i)
            {
                Console.Write(' ');
            }

            _maxLength = max;
        }

        private void SetCursor(int pos)
        {
            if (_cursor == pos)
            {
                return;
            }

            _cursor = pos;

            Console.SetCursorPosition(_cursor + Prompt.Length, Console.CursorTop);
        }

        public string ReadLine(string prompt = "")
        {
            Prompt = prompt;
            _lineText = new StringBuilder();
            _cursor = 0;
            _maxLength = 0;

            Refresh();

            _finished = false;
            ConsoleKeyInfo keyInfo;

            while (!_finished)
            {
                keyInfo = Console.ReadKey(intercept: true);

                Handler handler;
                if (s_keyHandlers.TryGetValue(keyInfo.Key, out handler) && 
                    keyInfo.Modifiers == handler.KeyInfo.Modifiers)
                {
                    handler.KeyHandler();
                    continue;
                }

                TypeChar(keyInfo.KeyChar);
            }

            Console.WriteLine();

            return _lineText == null ? null : _lineText.ToString();
        }
    }

	public class Test
    {
		public static void Main()
		{
			LineReader reader = new LineReader();
			string s;
			
			while ((s = reader.ReadLine("> ")) != null)
            {
				Console.WriteLine ("----> [{0}]", s);
			}
		}
	}
}