using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace sb.Tests;
using NUnit.Framework;

[TestFixture]
    public class LatexPageTests
    {
        private IEnumerable<Song> _songs = null!;
        
        [SetUp]
        public void SetUp() {
            _songs = TestUtilities.PopulateAsSongs();
        }

        [Test]
        public void SongPagePermutationsTest() {
            List<Song> songs = _songs.ToList();
            Trace.WriteLine(songs.Count);
            Trace.WriteLine(PermutationsAndCombinations.nCr(songs.Count, 2));
            Trace.WriteLine(PermutationsAndCombinations.nCr(songs.Count, 3));
            Trace.WriteLine(PermutationsAndCombinations.nCr(songs.Count, 4));
            Assert.Pass();
        }

        [Test]
        public void CreateCombinationsListTest() {

            List<Song[]> combs = _songs.CreateCombinationsList().ToList();

            for (int index = 0; index < combs.Count; index++) {
                Song[] songs = combs[index];
                Trace.WriteLine("Combination #" + index);
                foreach (Song song in songs) {
                    Trace.WriteLine(song.title);
                }

                Trace.WriteLine("");
            }

            Assert.Pass();
        }
        
        [Test]
        public void CreateRelevantCombinationsListTest() {

            List<Song[]> combs = _songs.CreateRelevantCombinationsList().ToList();

            for (int index = 0; index < combs.Count; index++) {
                Song[] songs = combs[index];
                Trace.WriteLine("Combination #" + index);
                foreach (Song song in songs) {
                    Trace.WriteLine("   " + song.title);
                }

                Trace.WriteLine("");
            }

            Assert.Pass();
        }
        
        [Test]
        public void TestBestFitColumn() {
            List<LatexPage> pages = LatexPageFormatter.FindOptimalFitForMinipages(_songs.ToArray());
            
            foreach (LatexPage latexPage in pages) {
                Trace.WriteLine("Page #" + (pages.IndexOf(latexPage) + 1));
                foreach (Song latexPageSong in latexPage.Songs) {
                    Trace.WriteLine(latexPageSong.title + " : " + latexPageSong.lineCount);
                }
            }
            Assert.Pass();
        }

        [Test]
        public void TestBestFitStandalone() {
            var pages = LatexPageFormatter.FindOptimalFitForStandalonePages(_songs.ToArray());
        }

        [Test]
        public void TestOutputStandalone() {
            string output = LatexPageFormatter.CreateOutputFromInput(LatexFormattingOption.Book, _songs.ToArray());
            Trace.WriteLine(output);
            Assert.Pass();
        }

        
    }