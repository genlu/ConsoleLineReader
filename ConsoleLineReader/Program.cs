﻿
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
        private string _prompt;
        private int PrintLength => _lineText == null || _prompt == null ? 0 : _lineText.Length + _prompt.Length;

        private bool _finished;

        private History _history;

        /// <summary>
        /// This is the index to the _lineText that corresponding to current cursor location in console buffer.
        /// </summary>
        private int Cursor { set; get; }

        // following two values need to be updated whenever buffer width is changed
        private int FirstRow { set; get; }
        private int BufferWidth { set; get; }

        private int RelativeTop => (Cursor + _prompt.Length) / BufferWidth + FirstRow;
        private int RelativeLeft => (Cursor + _prompt.Length) % BufferWidth;

        private int MaxTextLength { set; get; }

        private int RowCount => _lineText == null ? 0 : (_lineText.Length + _prompt.Length) / BufferWidth + 1;

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
            _history = new History();

            s_keyHandlers = new Dictionary<ConsoleKey, Handler>();

            s_keyHandlers[ConsoleKey.Escape] = new Handler(ConsoleKey.Escape, Escape);
            s_keyHandlers[ConsoleKey.Home] = new Handler(ConsoleKey.Home, Home);
            s_keyHandlers[ConsoleKey.End] = new Handler(ConsoleKey.End, End);
            s_keyHandlers[ConsoleKey.LeftArrow] = new Handler(ConsoleKey.LeftArrow, LeftArrow);
            s_keyHandlers[ConsoleKey.RightArrow] = new Handler(ConsoleKey.RightArrow, RightArrow);
            s_keyHandlers[ConsoleKey.UpArrow] = new Handler(ConsoleKey.UpArrow, UpArrow);
            s_keyHandlers[ConsoleKey.DownArrow] = new Handler(ConsoleKey.DownArrow, DownArrow);
            s_keyHandlers[ConsoleKey.Backspace] = new Handler(ConsoleKey.Backspace, Backspace);
            s_keyHandlers[ConsoleKey.Delete] = new Handler(ConsoleKey.Delete, Delete);
            s_keyHandlers[ConsoleKey.Enter] = new Handler(ConsoleKey.Enter, Enter);
            s_keyHandlers[ConsoleKey.Tab] = new Handler(ConsoleKey.Tab, () => { });
        }

        private void InitLine(string prompt)
        {
            _lineText = new StringBuilder();
            Cursor = 0;
            FirstRow = Console.CursorTop;
            BufferWidth = Console.BufferWidth;
            MaxTextLength = 0;
            _prompt = prompt;
            _finished = false;
        }

        private void Escape()
        {
            _lineText.Clear();
            Refresh();
            SetCursorPosition(0);
        }

        private void Home()
        {
            SetCursorPosition(0);
        }

        private void End()
        {
            SetCursorPosition(_lineText.Length);
        }

        private void LeftArrow()
        {
            if (Cursor == 0)
            {
                return;
            }
            SetCursorPosition(Cursor - 1);
        }

        private void RightArrow()
        {
            if (Cursor == _lineText.Length)
            {
                return;
            }
            SetCursorPosition(Cursor + 1);
        }

        private void UpArrow()
        {
            var prevEntry = _history.Previous();
            if (prevEntry == null)
            {
                return;
            }
            _lineText.Clear();
            _lineText.Append(prevEntry);
            Refresh();
            SetCursorPosition(_lineText.Length); 
        }

        private void DownArrow()
        {
            var nextEntry = _history.Next();
            if (nextEntry == null)
            {
                return;
            }
            _lineText.Clear();
            _lineText.Append(nextEntry);
            Refresh();
            SetCursorPosition(_lineText.Length);
        }

        private void Backspace()
        {
            if (Cursor == 0)
            {
                return;
            }

            var prev = Cursor - 1;
            _lineText.Remove(prev, 1);
            Refresh();
            SetCursorPosition(prev);
        }

        private void Delete()
        {
            if (Cursor == _lineText.Length)
            {
                return;
            }

            _lineText.Remove(Cursor, 1);
            Refresh();
            SetCursorPosition(Cursor);
        }

        private void Enter()
        {
            _finished = true;
        }

        private void InsertChar(char c)
        {
            _lineText.Insert(Cursor, c);
            Refresh();
            SetCursorPosition(Cursor + 1);
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
            int max = Math.Max(MaxTextLength, PrintLength);

            // todo: de we need to keep track of the actual first column instead of using 0?
            Console.SetCursorPosition(0, FirstRow);
            Console.Write(_prompt);
            Console.Write(_lineText.ToString());

            // clear the rest of the line
            for (int i = PrintLength; i < MaxTextLength; ++i)
            {
                Console.Write(' ');
            }

            MaxTextLength = max;
        }


        /// <summary>
        /// !Critical!
        /// All other cnosole buffer info relies on the correct update here.
        /// </summary>
        private void UpdateBufferInfo()
        {
            if (BufferWidth == Console.BufferWidth)
            {
                return;
            }
            BufferWidth = Console.BufferWidth;
            FirstRow = Console.CursorTop - Cursor / BufferWidth; 
        }

        /// <summary>
        /// Set cursor position in console
        /// </summary>
        /// <param name="pos">the index into the input string builder (which doesn't include prompt), that corresponding to new cursor position</param>
        private void SetCursorPosition(int pos)
        {
            Cursor = pos;
            Console.SetCursorPosition(RelativeLeft, RelativeTop);
        }

        public string ReadLine(string prompt = "")
        {
            InitLine(prompt);        
            Refresh();
            ConsoleKeyInfo keyInfo;

            while (!_finished)
            {
                keyInfo = Console.ReadKey(intercept: true);
                UpdateBufferInfo();

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
            var text = _lineText?.ToString();
            if (text != null)
            {
                _history.Add(text);
            }
            return text;
        }

        private class History
        {
            private List<string> _history;
            private int _current;

            public int MaxCount { get; private set; }

            public History(int maxLength = 50)
            {
                MaxCount = maxLength;
                _history = new List<string>();
                _current = -1;
            }

            public void Clear()
            {
                _history.Clear();
                _current = -1;
            }

            public int Count => _history.Count;

            private string Last => Count == 0 ? null : _history[Count - 1];

            public void Add(string text)
            {
                if (Last != text)
                {
                    _history.Add(text);
                }

                if (_current != -1 && _history[_current] != text)
                {
                    _current = -1;
                }

                if (Count > MaxCount)
                {
                    _history.RemoveAt(0);
                    if (_current > 0)
                    {
                        _current--;
                    }
                }
            }

            public string Next()
            {
                if ( _current == -1 || _current + 1 == Count)
                {
                    _current = -1;
                    return null;
                }
                return _history[++_current];
            }

            public string Previous()
            {
                if (Count == 0 || _current == 0)
                {
                    return null;
                }
                if (_current == -1)
                {
                    _current = Count;
                }
                return _history[--_current];
            }
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