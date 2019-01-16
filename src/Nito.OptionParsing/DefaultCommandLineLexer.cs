﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nito.OptionParsing
{
    /// <summary>
    /// Provides the default lexer for command lines, based on Raymond Chen's analysis here: http://blogs.msdn.com/b/oldnewthing/archive/2010/09/17/10063629.aspx
    /// </summary>
    public sealed class DefaultCommandLineLexer: ICommandLineLexer
    {
        public static DefaultCommandLineLexer Instance { get; } = new DefaultCommandLineLexer();

        private enum LexerState
        {
            /// <summary>
            /// The default state; no data exists in the argument character buffer.
            /// </summary>
            Default,

            /// <summary>
            /// An argument has been started.
            /// </summary>
            Argument,

            /// <summary>
            /// A quote character has been seen, and we are now parsing quoted data.
            /// </summary>
            Quoted,

            /// <summary>
            /// The quote has just been closed, but the argument is still being parsed.
            /// </summary>
            EndQuotedArgument
        }

        /// <summary>
        /// Lexes the command line, using double-quotes for quotation and consecutive double-quotes to embed double-quotes within a quotation. This lexer does not treat backslashes specially.
        /// </summary>
        /// <param name="commandLine">The command line to parse. May not be <c>null</c>.</param>
        /// <returns>The lexed command line.</returns>
        public IEnumerable<string> Lex(string commandLine)
        {
            if (commandLine == null)
                throw new ArgumentNullException(nameof(commandLine));

            // This algorithm assumes that no surrogate characters will represent whitespace.

            var result = string.Empty;
            var state = LexerState.Default;
            foreach (var ch in commandLine)
            {
                switch (state)
                {
                    case LexerState.Default:
                        // We do not have anything in the buffer at this point. `result == ""`
                        if (ch == '"')
                        {
                            // Enter the quoted state, without placing anything in the buffer.
                            state = LexerState.Quoted;
                        }
                        else if (!char.IsWhiteSpace(ch))
                        {
                            // Place the character into the buffer and enter the argument state.
                            result += ch;
                            state = LexerState.Argument;
                        }

                        // Otherwise, the character is whitespace, and it is just ignored.
                        break;

                    case LexerState.Argument:
                        // We have an argument started, though it may be just an empty string for now.
                        if (ch == '"')
                        {
                            // Enter the quoted state, without placing anything in the buffer.
                            state = LexerState.Quoted;
                        }
                        else if (char.IsWhiteSpace(ch))
                        {
                            // Whitespace ends this argument, so publish it and restart in the default state.
                            yield return result;
                            result = string.Empty;
                            state = LexerState.Default;
                        }
                        else
                        {
                            // Any non-quote, non-whitespace character is appended to the argument buffer.
                            result += ch;
                        }

                        break;

                    case LexerState.Quoted:
                        // We are within quotes, but may already have characters in the argument buffer.
                        if (ch == '"')
                        {
                            // A quote places us into a special state used to detect double double-quotes.
                            state = LexerState.EndQuotedArgument;
                        }
                        else
                        {
                            // Any non-quote character (including whitespace) is appended to the argument buffer.
                            result += ch;
                        }

                        break;

                    case LexerState.EndQuotedArgument:
                        // This is a special state that is treated like Argument or Quoted depending on whether the next character is a quote. It's not possible to stay in this state.
                        if (ch == '"')
                        {
                            // We just read a double double-quote within a quoted context, e.g., <c>"test ""</c>, so we add the quote to the buffer and re-enter the quoted state.
                            result += ch;
                            state = LexerState.Quoted;
                        }
                        else if (char.IsWhiteSpace(ch))
                        {
                            // In this case, the double-quote we just read did in fact end the quotation, so we publish the argument and restart in the default state.
                            yield return result;
                            result = string.Empty;
                            state = LexerState.Default;
                        }
                        else
                        {
                            // If the double-quote is followed by a non-quote, non-whitespace character, then it's considered a continuation of the argument (leaving the quoted state).
                            result += ch;
                            state = LexerState.Argument;
                        }

                        break;
                }
            }

            // If we end in the middle of an argument (or even a quotation), then we just publish what we have.
            if (state != LexerState.Default)
            {
                yield return result;
            }
        }

        /// <summary>
        /// Takes a list of arguments to pass to a program, and quotes them. This method assumes the receiving program does not treat backslashes specially. This method does not quote or escape special shell characters.
        /// </summary>
        /// <param name="arguments">The arguments to quote (if necessary) and concatenate into a command line. May not be <c>null</c>.</param>
        /// <returns>The command line.</returns>
        public string Escape(IEnumerable<string> arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            // Escape each argument (if necessary) and join them with spaces.
            return string.Join(" ", arguments.Select(argument =>
            {
                // An argument does not need escaping if it does not have any whitespace or quote characters.
                if (!argument.Any(ch => char.IsWhiteSpace(ch) || ch == '"') && argument != string.Empty)
                {
                    return argument;
                }

                // To escape the argument, wrap it in double-quotes and double any existing double-quotes.
                var ret = new StringBuilder();
                ret.Append('"');
                foreach (var ch in argument)
                {
                    if (ch == '"')
                    {
                        ret.Append("\"\"");
                    }
                    else
                    {
                        ret.Append(ch);
                    }
                }

                ret.Append('"');
                return ret.ToString();
            }));
        }
    }
}
