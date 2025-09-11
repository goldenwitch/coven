// SPDX-License-Identifier: BUSL-1.1

namespace Coven.Spellcasting;

using System.Threading.Tasks;
using Coven.Core;
using Coven.Spellcasting.Spells;


public abstract class MagikUser<TIn, TOut, TGuide, TSpell, TTest> : IMagikBlock<TIn, TOut>
{
    private readonly TGuide _guideBook;
    private readonly TSpell _spellBook;
    private readonly TTest _testBook;

    public MagikUser(TGuide Guidebook, TSpell Spellbook, TTest Testbook)
    {
        _guideBook = Guidebook;
        _spellBook = Spellbook;
        _testBook = Testbook;
    }

    public Task<TOut> DoMagik(TIn input)
    {
        return InvokeMagik(input, _guideBook, _spellBook, _testBook);
    }

    protected abstract Task<TOut> InvokeMagik(
        TIn input,
        TGuide guidebook,
        TSpell spellbook,
        TTest testbook
    );
}