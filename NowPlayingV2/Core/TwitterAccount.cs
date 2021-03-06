﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CoreTweet;

namespace NowPlayingV2.Core
{
    public class TwitterAccount : AccountContainer
    {
        public Tokens AuthToken { get; set; }

        //Will be called on Json.NET deserialization
        private TwitterAccount()
        {
        }

        public TwitterAccount(Tokens token)
        {
            AuthToken = token;
            UpdateName();
        }

        public override string ID => AuthToken.ScreenName;

        public override int MaxTweetLength => 280;

        public override void UpdateCache()
        {
            AuthToken.Account.VerifyCredentials();
            UpdateName();
        }

        public override void UpdateStatus(string UpdateText)
        {
            AuthToken.Statuses.Update(status: UpdateText);
        }

        public override void UpdateStatus(string UpdateText, string base64image)
        {
            var imgresult = AuthToken.Media.Upload(media_data: base64image);
            AuthToken.Statuses.Update(status: UpdateText, media_ids: new[] {imgresult.MediaId});
        }

        public override int CountText(string text) => CountTextStatic(text);

        public static int CountTextStatic(string text)
        {
            //CRLF is counted as 2 chars(should be counted as 1 char)
            var repchar = text.Replace(Environment.NewLine, " ");
            var sinfo = new StringInfo(repchar);
            //count all chars as 2 chars
            var scount = sinfo.LengthInTextElements * 2;
            //count only Hankaku chars
            var regexcount = Regex.Matches(repchar, "[ -~｡-ﾟ]").Count;
            //minus Hankaku chars(Because all char are counted as 2 chars on the code above)
            scount -= regexcount;
            return scount;
        }

        private void UpdateName() => Name = AuthToken.Users.Show(user_id: AuthToken.UserId).Name;
    }
}