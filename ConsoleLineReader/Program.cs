
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
        private static Dictionary<ConsoleKey, Handler> s_keyHandlers;

        private StringBuilder _lineText;
        private string Prompt { set; get; }

        private bool Finished { set; get; }

        /// <summary>
        /// This is the index to the _lineText that corresponding to current cursor location in console buffer.
        /// </summary>
        private int Cursor { set; get; }
        private int FirstRow { set; get; }
        private int BufferWidth { set; get; }
        private int MaxTextLength { set; get; }

        private int RowCount => _lineText == null ? 0 : (_lineText.Length + Prompt.Length) / Console.BufferWidth + 1;

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
            if (Cursor == 0)
            {
                return;
            }
            SetCursor(Cursor - 1);
        }

        private void RightArrow()
        {
            if (Cursor == _lineText.Length)
            {
                return;
            }
            SetCursor(Cursor + 1);
        }

        private void Backspace()
        {
            if (Cursor == 0)
            {
                return;
            }

            _lineText.Remove(Cursor - 1, 1);
            Refresh();
            SetCursor(Cursor - 1);
        }

        private void Enter()
        {
            Finished = true;
        }

        private void InsertChar(char insert)
        {
            _lineText.Insert(Cursor, insert);
            Refresh();
            SetCursor(Cursor + 1);
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
            int max = Math.Max(MaxTextLength, len);

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(Prompt);
            Console.Write(_lineText.ToString());

            // clear the rest of the line
            for (int i = len; i < MaxTextLength; ++i)
            {
                Console.Write(' ');
            }

            MaxTextLength = max;
        }

        private void SetCursor(int pos)
        {
            if (Cursor == pos)
            {
                return;
            }

            Cursor = pos;

            Console.SetCursorPosition(Cursor + Prompt.Length, Console.CursorTop);
        }

        public string ReadLine(string prompt = "")
        {
            Prompt = prompt;
            _lineText = new StringBuilder();
            Cursor = 0;
            MaxTextLength = 0;

            Refresh();

            Finished = false;
            ConsoleKeyInfo keyInfo;

            while (!Finished)
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
                Console.WriteLine("----> [{0}]", s);
            }
        }
    }
}