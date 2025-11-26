/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// An implementation of <see cref="ISTDT002AProvider"/> that will cache by date option contracts returned by another option chain provider.
    /// </summary>
    public class CachingSTDT002AProvider : ISTDT002AProvider
    {
        private readonly ConcurrentDictionary<Symbol, STDT002ACacheEntry> _cache = new ConcurrentDictionary<Symbol, STDT002ACacheEntry>();
        private readonly ISTDT002AProvider _optionChainProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingSTDT002AProvider"/> class
        /// </summary>
        /// <param name="optionChainProvider"></param>
        public CachingSTDT002AProvider(ISTDT002AProvider optionChainProvider)
        {
            _optionChainProvider = optionChainProvider;
        }

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The option or the underlying symbol to get the option chain for.
        /// Providing the option allows targetting an option ticker different than the default e.g. SPXW</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            List<Symbol> symbols;

            STDT002ACacheEntry entry;
            if (!_cache.TryGetValue(symbol, out entry) || date.Date != entry.Date)
            {
                symbols = _optionChainProvider.GetOptionContractList(symbol, date.Date).ToList();
                _cache[symbol] = new STDT002ACacheEntry(date.Date, symbols);
            }
            else
            {
                symbols = entry.Symbols;
            }

            return symbols;
        }

        private class STDT002ACacheEntry
        {
            public DateTime Date { get; }
            public List<Symbol> Symbols { get; }

            public STDT002ACacheEntry(DateTime date, List<Symbol> symbols)
            {
                Date = date;
                Symbols = symbols;
            }
        }
    }
}
