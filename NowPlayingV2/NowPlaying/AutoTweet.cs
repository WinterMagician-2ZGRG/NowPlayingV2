﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NowPlayingV2.Core;

namespace NowPlayingV2.NowPlaying
{
    public class AutoTweet
    {
        public static AutoTweet Autotweetsigleton = new AutoTweet();
        private Config appconfig => ConfigStore.StaticConfig;
        private Task tweetTask;
        private AutoResetEvent resetevent = new AutoResetEvent(false);
        private CancellationTokenSource cancellationToken = new CancellationTokenSource();
        private SongInfo lastplayedsong;
        private DateTime lasttweettime;

        public void InitListner(PipeListener pl)
        {
            pl.OnMusicPlay += OnMusicPlay;
        }

        private void OnMusicPlay(SongInfo songInfo)
        {
            Task.Run(() =>
            {
                if (!appconfig.EnableAutoTweet) return;
                if (appconfig.EnablePostDelay && !(tweetTask?.IsCompleted ?? true))
                {
                    //cancel tweet job
                    Trace.WriteLine($"[AutoTweet PostDelay]Cancel tweet task start.");
                    cancellationToken.Cancel();
                    resetevent.Set();
                    tweetTask.Wait();
                    Trace.WriteLine($"[AutoTweet PostDelay]Cancel tweet task OK.");
                }

                //can't reuse this
                cancellationToken = new CancellationTokenSource();
                //start task
                tweetTask = Task.Run(() =>
                {
                    try
                    {
                        //check for album art
                        var enablealbumart = appconfig.EnableTweetWithAlbumArt;
                        if (appconfig.EnableNoAlbumArtworkOnSameAlbum)
                        {
                            if (songInfo.Album == lastplayedsong?.Album)
                            {
                                Trace.WriteLine($"[AutoTweet]Disabled album art tweet.(reason=same album)");
                                enablealbumart = false;
                            }
                        }

                        //same album check
                        if (appconfig.EnableNoTweetOnSameAlbum)
                        {
                            if (songInfo.Album == lastplayedsong?.Album)
                            {
                                Trace.WriteLine($"[AutoTweet]Canceled tweet.(reason=EnableNoTweetOnSameAlbum)");
                                return;
                            }
                        }

                        //post delay
                        if (appconfig.EnablePostDelay)
                        {
                            Trace.WriteLine(
                                $"[AutoTweet PostDelay]Waiting for {appconfig.PostDelaySecond * 1000}msec.");
                            resetevent.WaitOne(appconfig.PostDelaySecond * 1000);
                        }

                        //post delay(by last tweet time)
                        if (appconfig.EnableTimePostDelay)
                        {
                            if (lasttweettime != default(DateTime))
                            {
                                if (!(DateTime.Now - lasttweettime >=
                                      new TimeSpan(0, 0, appconfig.TimePostDelayMin, 0)))
                                {
                                    Trace.WriteLine($"[AutoTweet]Canceled tweet.(reason=EnableTimePostDelay)");
                                    return;
                                }
                            }
                        }

                        cancellationToken.Token.ThrowIfCancellationRequested();
                        lastplayedsong = (SongInfo) songInfo.Clone();
                        lasttweettime = DateTime.Now;

                        //tweet it!
                        Task.Run(() =>
                        {
                            try
                            {
                                //get account list(only enabled)
                                appconfig.accountList.Where(itm => itm.Enabled).ToList().ForEach((accCont) =>
                                {
                                    //make tweet string
                                    var tweettext = Tsumugi.TweetConverter.SongInfoToString(appconfig.TweetFormat,
                                        songInfo,
                                        appconfig.EnableAutoDeleteText140, accCont);
                                    if (accCont.CountText(tweettext) > accCont.MaxTweetLength)
                                        throw new Exception(
                                            $"[AutoTweet]Tweet text was over {accCont.MaxTweetLength} chars.");
                                    //tweet
                                    if (enablealbumart)
                                    {
                                        accCont.UpdateStatus(tweettext, songInfo.AlbumArtBase64);
                                    }
                                    else
                                    {
                                        accCont.UpdateStatus(tweettext);
                                    }

                                    Trace.WriteLine($"[AutoTweet]Sent tweet for account @{accCont.ID}.");
                                });
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine($"[AutoTweet]Tweet error.\n{ex.Message}");
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        Trace.WriteLine($"[AutoTweet] OperationCanceledException");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[AutoTweet] {ex.Message}");
                    }
                });
            });
        }

        public void StopAllTask()
        {
            cancellationToken.Cancel();
            resetevent.Set();
        }
    }
}