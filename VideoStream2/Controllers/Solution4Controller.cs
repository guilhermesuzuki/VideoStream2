using System;
using System.IO;
using System.Security;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Linq;

internal struct ByteRange
{
    internal long Offset;
    internal long Length;
}

internal static class HttpStatus
{
    internal const int Unauthorized = 401;
    internal const int Forbidden = 403;
    internal const int NotFound = 404;
}

namespace VideoStream2.Controllers
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.IO;
    using System.Collections;
    using System.Web.Hosting;
    using System.Web.Util;
    using System.Globalization;
    using System.Diagnostics.CodeAnalysis;
    using System.Security.Permissions;

    /*
    * Various string handling utilities
    */
    internal static class StringUtil
    {
        /*
         * Determines if the string ends with the specified character.
         * Fast, non-culture aware.  
         */
        internal static bool StringEndsWith(string s, char c)
        {
            int len = s.Length;
            return len != 0 && s[len - 1] == c;
        }

        /*
         * Determines if the first string starts with the second string, ignoring case.
         * Fast, non-culture aware.  
         */
        internal static bool StringStartsWithIgnoreCase(string s1, string s2)
        {
            if (String.IsNullOrEmpty(s1) || String.IsNullOrEmpty(s2))
            {
                return false;
            }

            if (s2.Length > s1.Length)
            {
                return false;
            }

            return 0 == string.Compare(s1, 0, s2, 0, s2.Length, StringComparison.OrdinalIgnoreCase);
        }

        internal unsafe static void memcpyimpl(byte* src, byte* dest, int len)
        {
            System.Diagnostics.Debug.Assert(len >= 0, "Negative length in memcpyimpl!");

#if FEATURE_PAL
        // Portable naive implementation
        while (len-- > 0)
            *dest++ = *src++;
#else
#if IA64
        long dstAlign = (7 & (8 - (((long)dest) & 7))); // number of bytes to copy before dest is 8-byte aligned
 
        while ((dstAlign > 0) && (len > 0))
        {
            // <
            *dest++ = *src++;
            
            len--;
            dstAlign--;
        }
 
        if (len > 0)
        {
            long srcAlign = 8 - (((long)src) & 7);
            if (srcAlign != 8)
            {
                if (4 == srcAlign)
                {
                    while (len >= 4)
                    {
                        ((int*)dest)[0] = ((int*)src)[0];
                        dest += 4;
                        src  += 4;
                        len  -= 4;
                    }
                    
                    srcAlign = 2;   // fall through to 2-byte copies
                }
                
                if ((2 == srcAlign) || (6 == srcAlign))
                {
                    while (len >= 2)
                    {
                        ((short*)dest)[0] = ((short*)src)[0];
                        dest += 2;
                        src  += 2;
                        len  -= 2;
                    }
                }
                
                while (len-- > 0)
                {
                    *dest++ = *src++;
                }
            }
            else
            {
                if (len >= 16) 
                {
                    do 
                    {
                        ((long*)dest)[0] = ((long*)src)[0];
                        ((long*)dest)[1] = ((long*)src)[1];
                        dest += 16;
                        src += 16;
                    } while ((len -= 16) >= 16);
                }
                
                if(len > 0)  // protection against negative len and optimization for len==16*N
                {
                    if ((len & 8) != 0) 
                    {
                        ((long*)dest)[0] = ((long*)src)[0];
                        dest += 8;
                        src += 8;
                    }
                    if ((len & 4) != 0) 
                    {
                        ((int*)dest)[0] = ((int*)src)[0];
                        dest += 4;
                        src += 4;
                    }
                    if ((len & 2) != 0) 
                    {
                        ((short*)dest)[0] = ((short*)src)[0];
                        dest += 2;
                        src += 2;
                    }
                    if ((len & 1) != 0)
                    {
                        *dest++ = *src++;
                    }
                }
            }
        }
#else
            // <





            if (len >= 16)
            {
                do
                {
#if AMD64
                ((long*)dest)[0] = ((long*)src)[0];
                ((long*)dest)[1] = ((long*)src)[1];
#else
                    ((int*)dest)[0] = ((int*)src)[0];
                    ((int*)dest)[1] = ((int*)src)[1];
                    ((int*)dest)[2] = ((int*)src)[2];
                    ((int*)dest)[3] = ((int*)src)[3];
#endif
                    dest += 16;
                    src += 16;
                } while ((len -= 16) >= 16);
            }
            if (len > 0)  // protection against negative len and optimization for len==16*N
            {
                if ((len & 8) != 0)
                {
#if AMD64
                ((long*)dest)[0] = ((long*)src)[0];
#else
                    ((int*)dest)[0] = ((int*)src)[0];
                    ((int*)dest)[1] = ((int*)src)[1];
#endif
                    dest += 8;
                    src += 8;
                }
                if ((len & 4) != 0)
                {
                    ((int*)dest)[0] = ((int*)src)[0];
                    dest += 4;
                    src += 4;
                }
                if ((len & 2) != 0)
                {
                    ((short*)dest)[0] = ((short*)src)[0];
                    dest += 2;
                    src += 2;
                }
                if ((len & 1) != 0)
                    *dest++ = *src++;
            }
#endif
#endif // FEATURE_PAL
        }
        internal static string[] ObjectArrayToStringArray(object[] objectArray)
        {
            String[] stringKeys = new String[objectArray.Length];
            objectArray.CopyTo(stringKeys, 0);
            return stringKeys;
        }

    }

    internal static class HttpDate
    {
        static readonly int[] s_tensDigit = new int[10] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90 };

        /*++
        
            Converts a 2 character string to integer
        
            Arguments:
                s   String to convert
        
            Returns:
                numeric equivalent, 0 on failure.
        --*/
        static int atoi2(string s, int startIndex)
        {
            try
            {
                int tens = s[0 + startIndex] - '0';
                int ones = s[1 + startIndex] - '0';

                return s_tensDigit[tens] + ones;
            }
            catch
            {
                throw new FormatException($"Atio2BadString: {s}, {startIndex}");
                //throw new FormatException(SR.GetString(SR.Atio2BadString, s, startIndex));
            }
        }

        static readonly string[] s_days = new string[7] {
            "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
        };

        static readonly string[] s_months = new string[12] {
            "Jan", "Feb", "Mar", "Apr",
            "May", "Jun", "Jul", "Aug",
            "Sep", "Oct", "Nov", "Dec"
        };

        // Custom table for make_month() for mapping "Apr" to 4
        static readonly sbyte[] s_monthIndexTable = new sbyte[64] {
           -1, (sbyte)'A',          2, 12, -1, -1,         -1,  8, // A to G
           -1,         -1,         -1, -1,  7, -1, (sbyte)'N', -1, // H to O
            9,         -1, (sbyte)'R', -1, 10, -1,         11, -1, // P to W
           -1,          5,         -1, -1, -1, -1,         -1, -1, // X to Z
           -1, (sbyte)'A',          2, 12, -1, -1,         -1,  8, // a to g
           -1,         -1,         -1, -1,  7, -1, (sbyte)'N', -1, // h to o
            9,         -1, (sbyte)'R', -1, 10, -1,         11, -1, // p to w
           -1,          5,         -1, -1, -1, -1,         -1, -1  // x to z
        };

        static int make_month(string s, int startIndex)
        {
            int i;
            sbyte monthIndex;
            string monthString;

            //
            // use the third character as the index
            //

            i = ((int)s[2 + startIndex] - 0x40) & 0x3F;
            monthIndex = s_monthIndexTable[i];

            if (monthIndex >= 13)
            {

                //
                // ok, we need to look at the second character
                //

                if (monthIndex == (sbyte)'N')
                {

                    //
                    // we got an N which we need to resolve further
                    //

                    //
                    // if s[1] is 'u' then Jun, if 'a' then Jan
                    //


                    if (s_monthIndexTable[(s[1 + startIndex] - 0x40) & 0x3f] == (sbyte)'A')
                    {
                        monthIndex = 1;
                    }
                    else
                    {
                        monthIndex = 6;
                    }

                }
                else if (monthIndex == (sbyte)'R')
                {

                    //
                    // if s[1] is 'a' then Microsoft, if 'p' then April
                    //

                    if (s_monthIndexTable[(s[1 + startIndex] - 0x40) & 0x3f] == (sbyte)'A')
                    {
                        monthIndex = 3;
                    }
                    else
                    {
                        monthIndex = 4;
                    }
                }
                else
                {
                    throw new FormatException($"MakeMonthBadString: {s}, {startIndex}");
                    //throw new FormatException(SR.GetString(SR.MakeMonthBadString, s, startIndex));
                }
            }


            monthString = s_months[monthIndex - 1];

            if ((s[0 + startIndex] == monthString[0]) &&
                 (s[1 + startIndex] == monthString[1]) &&
                 (s[2 + startIndex] == monthString[2]))
            {

                return (monthIndex);

            }
            else if (((Char.ToUpper(s[0 + startIndex], CultureInfo.InvariantCulture)) == monthString[0]) &&
                      ((Char.ToLower(s[1 + startIndex], CultureInfo.InvariantCulture)) == monthString[1]) &&
                      ((Char.ToLower(s[2 + startIndex], CultureInfo.InvariantCulture)) == monthString[2]))
            {

                return monthIndex;
            }

            throw new FormatException($"MakeMonthBadString: {s}, {startIndex}");
            //throw new FormatException(SR.GetString(SR.MakeMonthBadString, s, startIndex));

        } // make_month

        /*++
        
          Converts a string representation of a GMT time (three different
          varieties) to an NT representation of a file time.
        
          We handle the following variations:
        
            Sun, 06 Nov 1994 08:49:37 GMT   (RFC 822 updated by RFC 1123)
            Sunday, 06-Nov-94 08:49:37 GMT  (RFC 850)
            Sun Nov 06 08:49:37 1994        (ANSI C's asctime() format
        
          Arguments:
            time                String representation of time field
        
          Returns:
            TRUE on success and FALSE on failure.
        
          History:
        
            Johnl       24-Jan-1995     Modified from WWW library
        
        --*/
        static internal DateTime UtcParse(string time)
        {
            int i;
            int year, month, day, hour, minute, second;

            if (time == null)
            {
                throw new ArgumentNullException("time");
            }

            if ((i = time.IndexOf(',')) != -1)
            {

                //
                // Thursday, 10-Jun-93 01:29:59 GMT
                // or: Thu, 10 Jan 1993 01:29:59 GMT */
                //

                int length = time.Length - i;
                while (--length > 0 && time[++i] == ' ') ;

                if (time[i + 2] == '-')
                {      /* First format */

                    if (length < 18)
                    {
                        throw new FormatException($"UtilParseDateTimeBad: {time}");
                        //throw new FormatException(SR.GetString(SR.UtilParseDateTimeBad, time));
                    }

                    day = atoi2(time, i);
                    month = make_month(time, i + 3);
                    year = atoi2(time, i + 7);
                    if (year < 50)
                    {
                        year += 2000;
                    }
                    else
                    {
                        year += 1900;
                    }

                    hour = atoi2(time, i + 10);
                    minute = atoi2(time, i + 13);
                    second = atoi2(time, i + 16);

                }
                else
                {                         /* Second format */

                    if (length < 20)
                    {
                        throw new FormatException($"UtilParseDateTimeBad: {time}");
                        //throw new FormatException(SR.GetString(SR.UtilParseDateTimeBad, time));
                    }

                    day = atoi2(time, i);
                    month = make_month(time, i + 3);
                    year = atoi2(time, i + 7) * 100 + atoi2(time, i + 9);
                    hour = atoi2(time, i + 12);
                    minute = atoi2(time, i + 15);
                    second = atoi2(time, i + 18);
                }
            }
            else
            {    /* Try the other format:  Wed Jun 09 01:29:59 1993 GMT */

                i = -1;
                int length = time.Length + 1;
                while (--length > 0 && time[++i] == ' ') ;

                if (length < 24)
                {
                    throw new FormatException($"UtilParseDateTimeBad: {time}");
                    //throw new FormatException(SR.GetString(SR.UtilParseDateTimeBad, time));
                }

                day = atoi2(time, i + 8);
                month = make_month(time, i + 4);
                year = atoi2(time, i + 20) * 100 + atoi2(time, i + 22);
                hour = atoi2(time, i + 11);
                minute = atoi2(time, i + 14);
                second = atoi2(time, i + 17);
            }

            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }
    }

    public class Solution4Controller : Controller
    {
        [HttpGet]
        public void Index()
        {
            var relativePath = VirtualPathUtility.ToAbsolute(HomeController.harryPotter);
            ProcessRequestInternal(System.Web.HttpContext.Current, overrideVirtualPath: relativePath);
        }

        private const string RANGE_BOUNDARY = "<q1w2e3r4t5y6u7i8o9p0zaxscdvfbgnhmjklkl>";
        private const string MULTIPART_CONTENT_TYPE = "multipart/byteranges; boundary=<q1w2e3r4t5y6u7i8o9p0zaxscdvfbgnhmjklkl>";
        private const string MULTIPART_RANGE_DELIMITER = "--<q1w2e3r4t5y6u7i8o9p0zaxscdvfbgnhmjklkl>\r\n";
        private const string MULTIPART_RANGE_END = "--<q1w2e3r4t5y6u7i8o9p0zaxscdvfbgnhmjklkl>--\r\n\r\n";
        private const string CONTENT_RANGE_FORMAT = "bytes {0}-{1}/{2}";
        private const int MAX_RANGE_ALLOWED = 5;

        private const int ERROR_ACCESS_DENIED = 5;

        private static bool IsOutDated(string ifRangeHeader, DateTime lastModified)
        {
            try
            {
                DateTime utcLastModified = lastModified.ToUniversalTime();
                DateTime utc = HttpDate.UtcParse(ifRangeHeader);
                return (utc < utcLastModified);
            }
            catch
            {
                return true;
            }
        }

        private static string GenerateETag(HttpContext context, DateTime lastModified, DateTime now)
        {
            // Get 64-bit FILETIME stamp
            long lastModFileTime = lastModified.ToFileTime();
            long nowFileTime = now.ToFileTime();
            string hexFileTime = lastModFileTime.ToString("X8", CultureInfo.InvariantCulture);

            // Do what IIS does to determine if this is a weak ETag.
            // Compare the last modified time to now and if the difference is
            // less than or equal to 3 seconds, then it is weak
            if ((nowFileTime - lastModFileTime) <= 30000000)
            {
                return "W/\"" + hexFileTime + "\"";
            }
            return "\"" + hexFileTime + "\"";
        }

        private static FileInfo GetFileInfo(string virtualPathWithPathInfo, string physicalPath, HttpResponse response)
        {
            // Check whether the file exists
            if (!System.IO.File.Exists(physicalPath))
            {
                throw new HttpException(HttpStatus.NotFound, "File does not exist");
            }
            // To prevent the trailing dot problem, error out all file names with trailing dot.
            if (physicalPath[physicalPath.Length - 1] == '.')
            {
                throw new HttpException(HttpStatus.NotFound, "File does not exist");
            }

            #region HttpRuntime.HasFilePermission

            var hasFilePermission = false;

            var hasFilePermissionMethodInfo = typeof(HttpRuntime)
                    .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Where(x => x.Name.Equals("HasFilePermission"))
                    .Select(m => new { Method = m, Parameters = m.GetParameters() })
                    .FirstOrDefault(p =>
                        p.Parameters.Length == 1
                        && p.Parameters[0].ParameterType.Name.ToLower() == "string"
                    ).Method;

            if (hasFilePermissionMethodInfo != null)
            {
                hasFilePermission = (bool)hasFilePermissionMethodInfo.Invoke(null, new object[1] { physicalPath });
            }

            #endregion

            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(physicalPath);
            }
            catch (IOException ioEx)
            {
                if (!hasFilePermission)
                    throw new HttpException(HttpStatus.NotFound, "Error trying to enumerate files.");
                else
                    throw new HttpException(HttpStatus.NotFound, "Error trying to enumerate files.", ioEx);
            }
            catch (SecurityException secEx)
            {
                if (!hasFilePermission)
                    throw new HttpException(HttpStatus.Unauthorized, "File enumerator access denied");
                else
                    throw new HttpException(HttpStatus.Unauthorized, "File enumerator access denied", secEx);
            }

            // To be consistent with IIS, we won't serve out hidden files
            if ((((int)fileInfo.Attributes) & ((int)FileAttributes.Hidden)) != 0)
            {
                throw new HttpException(HttpStatus.NotFound, "File is hidden");
            }

            // If the file is a directory, then it must not have a slash in
            // end of it (if it does have a slash suffix, then the config file
            // mappings are missing and we will just return 403.  Otherwise, 
            // we will redirect the client to the URL with this slash.
            if ((((int)fileInfo.Attributes) & ((int)FileAttributes.Directory)) != 0)
            {
                if (StringUtil.StringEndsWith(virtualPathWithPathInfo, '/'))
                {
                    // Just return 403
                    throw new HttpException(HttpStatus.Forbidden, "Missing star mapping");
                }
                else
                {
                    // Redirect to a slash suffixed URL which will be 
                    // handled by the */ handler mapper
                    response.Redirect(virtualPathWithPathInfo + "/");
                }
            }

            return fileInfo;
        }

        // initial space characters are skipped, and the string of digits up until the first non-digit
        // are converted to a long.  If digits are found the method returns true; otherwise false.
        private static bool GetLongFromSubstring(string s, ref int startIndex, out long result)
        {
            result = 0;

            // get index of first digit
            MovePastSpaceCharacters(s, ref startIndex);
            int beginIndex = startIndex;

            // get index of last digit
            MovePastDigits(s, ref startIndex);
            int endIndex = startIndex - 1;

            // are there any digits?
            if (endIndex < beginIndex)
            {
                return false;
            }

            long multipleOfTen = 1;
            for (int i = endIndex; i >= beginIndex; i--)
            {
                int digit = s[i] - '0';
                result += digit * multipleOfTen;
                multipleOfTen *= 10;
                // check for overflow
                if (result < 0)
                {
                    return false;
                }
            }
            return true;
        }

        // The Range header consists of one or more byte range specifiers.  E.g, "Range: bytes=0-1024,-1024" is a request
        // for the first and last 1024 bytes of a file. Before this method is called, startIndex points to the beginning
        // of a byte range specifier; and afterwards it points to the beginning of the next byte range specifier.  
        // If the current byte range specifier is syntactially inavlid, this function will return false indicating that the 
        // Range header must be ignored.  If the function returns true, then the byte range specifier will be converted to 
        // an offset and length, and the startIndex will be incremented to the next byte range specifier.  The byte range 
        // specifier (offset and length) returned by this function is satisfiable if and only if isSatisfiable is true.
        private static bool GetNextRange(string rangeHeader, ref int startIndex, long fileLength, out long offset, out long length, out bool isSatisfiable)
        {
            // startIndex is first char after '=', or first char after ','
            System.Diagnostics.Debug.Assert(startIndex < rangeHeader.Length, "startIndex < rangeHeader.Length");

            offset = 0;
            length = 0;
            isSatisfiable = false;

            // A Range request to an empty file is never satisfiable, and will always receive a 416 status.
            if (fileLength <= 0)
            {
                // put startIndex at end of string so we don't try to call GetNextRange again
                startIndex = rangeHeader.Length;
                return true;
            }

            MovePastSpaceCharacters(rangeHeader, ref startIndex);

            if (startIndex < rangeHeader.Length && rangeHeader[startIndex] == '-')
            {
                // this range is of the form "-mmm"
                startIndex++;
                if (!GetLongFromSubstring(rangeHeader, ref startIndex, out length))
                {
                    return false;
                }
                if (length > fileLength)
                {
                    // send entire file
                    offset = 0;
                    length = fileLength;
                }
                else
                {
                    // send last N bytes
                    offset = fileLength - length;
                }
                isSatisfiable = IsRangeSatisfiable(offset, length, fileLength);
                // we parsed the current range, but need to successfully move the startIndex to the next range
                return IncrementToNextRange(rangeHeader, ref startIndex);
            }
            else
            {
                // this range is of the form "nnn-[mmm]"
                if (!GetLongFromSubstring(rangeHeader, ref startIndex, out offset))
                {
                    return false;
                }
                // increment startIndex past '-'
                if (startIndex < rangeHeader.Length && rangeHeader[startIndex] == '-')
                {
                    startIndex++;
                }
                else
                {
                    return false;
                }
                long endPos;
                if (!GetLongFromSubstring(rangeHeader, ref startIndex, out endPos))
                {
                    // assume range is of form "nnn-".  If it isn't,
                    // the call to IncrementToNextRange will return false
                    length = fileLength - offset;
                }
                else
                {
                    // if...greater than or equal to the current length of the entity-body, last-byte-pos 
                    // is taken to be equal to one less than the current length of the entity- body in bytes.
                    if (endPos > fileLength - 1)
                    {
                        endPos = fileLength - 1;
                    }

                    length = endPos - offset + 1;

                    if (length < 1)
                    {
                        // the byte range specifier is syntactially invalid 
                        // because the last-byte-pos < first-byte-pos
                        return false;
                    }
                }
                isSatisfiable = IsRangeSatisfiable(offset, length, fileLength);
                // we parsed the current range, but need to successfully move the startIndex to the next range      
                return IncrementToNextRange(rangeHeader, ref startIndex);
            }
        }

        private static bool IncrementToNextRange(string s, ref int startIndex)
        {
            // increment startIndex until next token and return true, unless the syntax is invalid
            MovePastSpaceCharacters(s, ref startIndex);
            if (startIndex < s.Length)
            {
                if (s[startIndex] != ',')
                {
                    return false;
                }
                // move to first char after ','
                startIndex++;
            }
            return true;
        }

        private static bool IsRangeSatisfiable(long offset, long length, long fileLength)
        {
            return (offset < fileLength && length > 0);
        }

        private static bool IsSecurityError(int ErrorCode)
        {
            return (ErrorCode == ERROR_ACCESS_DENIED);
        }

        private static void MovePastSpaceCharacters(string s, ref int startIndex)
        {
            while (startIndex < s.Length && s[startIndex] == ' ')
            {
                startIndex++;
            }
        }

        private static void MovePastDigits(string s, ref int startIndex)
        {
            while (startIndex < s.Length && s[startIndex] <= '9' && s[startIndex] >= '0')
            {
                startIndex++;
            }
        }

        private static bool ProcessRequestForNonMapPathBasedVirtualFile(HttpRequest request, HttpResponse response, string overrideVirtualPath)
        {
            bool handled = false;

            // only process custom virtual path providers here
            var usingMapPathBasedVirtualPathProvider = typeof(HostingEnvironment).GetProperty(
                "UsingMapPathBasedVirtualPathProvider", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (usingMapPathBasedVirtualPathProvider != null && usingMapPathBasedVirtualPathProvider.CanRead)
            {
                var value = (bool)usingMapPathBasedVirtualPathProvider.GetValue(null);
                if (value) return handled;
            }

            VirtualFile virtualFile = null;
            String virtualPath = (overrideVirtualPath == null) ? request.FilePath : overrideVirtualPath;

            if (HostingEnvironment.VirtualPathProvider.FileExists(virtualPath))
            {
                virtualFile = HostingEnvironment.VirtualPathProvider.GetFile(virtualPath);
            }

            if (virtualFile == null)
            {
                Trace("StaticFileHandler", "Virtual file " + virtualPath + " not found");

                throw new HttpException(HttpStatus.NotFound, "File does not exist.");

                /*throw new HttpException(HttpStatus.NotFound, 
                                         SR.GetString(SR.File_does_not_exist));*/
            }

            // if we have a MapPathBasedVirtualFile, we can handle it the normal way
            if (virtualFile.GetType().Name == "MapPathBasedVirtualFile")
            {
                return handled;
            }

            Trace("StaticFileHandler", "Using VirtualPathProvider for " + virtualPath);

            //response.WriteVirtualFile(virtualFile);

            var writeVirtualFile = response.GetType().GetMethod("WriteVirtualFile", System.Reflection.BindingFlags.NonPublic);
            if (writeVirtualFile != null)
            {
                writeVirtualFile.Invoke(response, new object[1] { virtualFile });
            }

            response.ContentType = MimeMapping.GetMimeMapping(virtualPath);
            handled = true;
            return handled;
        }

        internal static bool ProcessRangeRequest(HttpContext context,
                                                 string physicalPath,
                                                 long fileLength,
                                                 string rangeHeader,
                                                 string etag,
                                                 DateTime lastModified)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;
            bool handled = false;

            // return "416 Requested range not satisfiable" if the file length is zero.
            if (fileLength <= 0)
            {
                SendRangeNotSatisfiable(response, fileLength);
                handled = true;
                return handled;
            }

            string ifRangeHeader = request.Headers["If-Range"];
            if (ifRangeHeader != null && ifRangeHeader.Length > 1)
            {
                // Is this an ETag or a Date? We only need to check two 
                // characters; an ETag either begins with W/ or it is quoted.
                if (ifRangeHeader[0] == '"')
                {
                    // it's a strong ETag
                    if (ifRangeHeader != etag)
                    {
                        // the etags do not match, and we will therefore return the entire response
                        return handled;
                    }
                }
                else if (ifRangeHeader[0] == 'W' && ifRangeHeader[1] == '/')
                {
                    // it's a weak ETag, and is therefore not usable for sub-range retrieval and
                    // we will return the entire response
                    return handled;
                }
                else
                {
                    // It's a date. If it is greater than or equal to the last-write time of the file, we can send the range.
                    if (IsOutDated(ifRangeHeader, lastModified))
                    {
                        return handled;
                    }
                }
            }

            // the expected format is "bytes = <range1>[, <range2>, ...]"
            // where <range> is "<first_byte_pos>-[<last_byte_pos>]" or "-<last_n_bytes>".
            int indexOfEquals = rangeHeader.IndexOf('=');
            if (indexOfEquals == -1 || indexOfEquals == rangeHeader.Length - 1)
            {
                //invalid syntax
                return handled;
            }

            // iterate through the byte ranges and write each satisfiable range to the response
            int startIndex = indexOfEquals + 1;
            bool isRangeHeaderSyntacticallyValid = true;
            long offset;
            long length;
            bool isSatisfiable;
            bool exceededMax = false;
            ByteRange[] byteRanges = null;
            int byteRangesCount = 0;
            long totalBytes = 0;
            while (startIndex < rangeHeader.Length && isRangeHeaderSyntacticallyValid)
            {
                isRangeHeaderSyntacticallyValid = GetNextRange(rangeHeader, ref startIndex, fileLength, out offset, out length, out isSatisfiable);
                if (!isRangeHeaderSyntacticallyValid)
                {
                    break;
                }
                if (!isSatisfiable)
                {
                    continue;
                }
                if (byteRanges == null)
                {
                    byteRanges = new ByteRange[16];
                }
                if (byteRangesCount >= byteRanges.Length)
                {
                    // grow byteRanges array
                    ByteRange[] buffer = new ByteRange[byteRanges.Length * 2];
                    int byteCount = byteRanges.Length * Marshal.SizeOf(byteRanges[0]);
                    unsafe
                    {
                        fixed (ByteRange* src = byteRanges, dst = buffer)
                        {
                            StringUtil.memcpyimpl((byte*)src, (byte*)dst, byteCount);
                        }
                    }
                    byteRanges = buffer;
                }
                byteRanges[byteRangesCount].Offset = offset;
                byteRanges[byteRangesCount].Length = length;
                byteRangesCount++;
                // IIS imposes this limitation too, and sends "400 Bad Request" if exceeded
                totalBytes += length;
                if (totalBytes > fileLength * MAX_RANGE_ALLOWED)
                {
                    exceededMax = true;
                    break;
                }
            }

            if (!isRangeHeaderSyntacticallyValid)
            {
                return handled;
            }

            if (exceededMax)
            {
                SendBadRequest(response);
                handled = true;
                return handled;
            }

            if (byteRangesCount == 0)
            {
                // we parsed the Range header and found no satisfiable byte ranges, so return "416 Requested Range Not Satisfiable"
                SendRangeNotSatisfiable(response, fileLength);
                handled = true;
                return handled;
            }

            string contentType = MimeMapping.GetMimeMapping(physicalPath);
            if (byteRangesCount == 1)
            {
                offset = byteRanges[0].Offset;
                length = byteRanges[0].Length;
                response.ContentType = contentType;
                string contentRange = String.Format(CultureInfo.InvariantCulture, CONTENT_RANGE_FORMAT, offset, offset + length - 1, fileLength);
                response.AppendHeader("Content-Range", contentRange);

                SendFile(physicalPath, offset, length, fileLength, context);
            }
            else
            {
                response.ContentType = MULTIPART_CONTENT_TYPE;
                string contentRange;
                string partialContentType = "Content-Type: " + contentType + "\r\n";
                for (int i = 0; i < byteRangesCount; i++)
                {
                    offset = byteRanges[i].Offset;
                    length = byteRanges[i].Length;
                    response.Write(MULTIPART_RANGE_DELIMITER);
                    response.Write(partialContentType);
                    response.Write("Content-Range: ");
                    contentRange = String.Format(CultureInfo.InvariantCulture, CONTENT_RANGE_FORMAT, offset, offset + length - 1, fileLength);
                    response.Write(contentRange);
                    response.Write("\r\n\r\n");
                    SendFile(physicalPath, offset, length, fileLength, context);
                    response.Write("\r\n");
                }
                response.Write(MULTIPART_RANGE_END);
            }

            // if we make it here, we're sending a "206 Partial Content" status
            response.StatusCode = 206;

            var formatHttpDateTimeMethodInfo = typeof(HttpUtility).GetMethod(
                "FormatHttpDateTime", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (formatHttpDateTimeMethodInfo != null)
            {
                response.AppendHeader("Last-Modified", (string)formatHttpDateTimeMethodInfo.Invoke(null, new object[1] { lastModified }));
            }

            response.AppendHeader("Accept-Ranges", "bytes");
            response.AppendHeader("ETag", etag);
            response.AppendHeader("Cache-Control", "public");

            handled = true;
            return handled;
        }

        internal static void ProcessRequestInternal(HttpContext context, string overrideVirtualPath)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;
            string virtualPathWithPathInfo;
            string physicalPath;
            FileInfo fileInfo;
            long fileLength;
            DateTime lastModifiedInUtc;
            string etag;
            string rangeHeader;

            // custom virtual path providers that don't yeild a MapPathBasedVirtualFile 
            // are a special case, and do not support TransmitFile, WriteFile, Range requests, 
            // or the cache policy that we apply below
            if (ProcessRequestForNonMapPathBasedVirtualFile(request, response, overrideVirtualPath))
            {
                return;
            }

            if (overrideVirtualPath == null)
            {
                virtualPathWithPathInfo = request.Path;
                physicalPath = request.PhysicalPath;
            }
            else
            {
                virtualPathWithPathInfo = overrideVirtualPath;
                physicalPath = request.MapPath(overrideVirtualPath);
            }

            Trace("StaticFileHandler", "Path= " + virtualPathWithPathInfo + ", PhysicalPath= " + physicalPath);

            fileInfo = GetFileInfo(virtualPathWithPathInfo, physicalPath, response);

            // Determine Last Modified Time.  We might need it soon 
            // if we encounter a Range: and If-Range header
            // Using UTC time to avoid daylight savings time bug 83230
            lastModifiedInUtc = new DateTime(fileInfo.LastWriteTimeUtc.Year,
                                        fileInfo.LastWriteTimeUtc.Month,
                                        fileInfo.LastWriteTimeUtc.Day,
                                        fileInfo.LastWriteTimeUtc.Hour,
                                        fileInfo.LastWriteTimeUtc.Minute,
                                        fileInfo.LastWriteTimeUtc.Second,
                                        0,
                                        DateTimeKind.Utc);

            // Because we can't set a "Last-Modified" header to any time
            // in the future, check the last modified time and set it to
            // DateTime.Now if it's in the future. 
            // This is to fix VSWhidbey #402323
            DateTime utcNow = DateTime.UtcNow;
            if (lastModifiedInUtc > utcNow)
            {
                // use 1 second resolution
                lastModifiedInUtc = new DateTime(utcNow.Ticks - (utcNow.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
            }

            etag = GenerateETag(context, lastModifiedInUtc, utcNow);
            fileLength = fileInfo.Length;

            // is this a Range request?
            rangeHeader = request.Headers["Range"];
            if (StringUtil.StringStartsWithIgnoreCase(rangeHeader, "bytes")
                && ProcessRangeRequest(context,
                                       physicalPath,
                                       fileLength,
                                       rangeHeader,
                                       etag,
                                       lastModifiedInUtc))
            {
                return;
            }

            // if we get this far, we're sending the entire file
            SendFile(physicalPath, 0, fileLength, fileLength, context);

            // Specify content type. Use extension to do the mapping
            response.ContentType = MimeMapping.GetMimeMapping(physicalPath);
            // Static file handler supports byte ranges
            response.AppendHeader("Accept-Ranges", "bytes");
            // We want to flush cache entry when static file has changed
            response.AddFileDependency(physicalPath);
            // Set IgnoreRangeRequests to avoid serving Range requests from the output cache.
            // Note that the kernel cache always ignores Range requests.

            //response.Cache.SetIgnoreRangeRequests();
            var setIgnoreRangeRequests = response.GetType().GetMethod("SetIgnoreRangeRequests", System.Reflection.BindingFlags.NonPublic);
            if (setIgnoreRangeRequests != null)
            {
                setIgnoreRangeRequests.Invoke(response, null);
            }

            // Set an expires in the future.
            response.Cache.SetExpires(utcNow.AddDays(1));
            // always set Last-Modified
            response.Cache.SetLastModified(lastModifiedInUtc);
            // always set ETag
            response.Cache.SetETag(etag);
            // always set Cache-Control to public
            response.Cache.SetCacheability(HttpCacheability.Public);
        }

        private static void SendBadRequest(HttpResponse response)
        {
            response.StatusCode = 400;
            response.Write("<html><body>Bad Request</body></html>");
        }

        private static void SendRangeNotSatisfiable(HttpResponse response, long fileLength)
        {
            response.StatusCode = 416;
            response.ContentType = null;
            response.AppendHeader("Content-Range", "bytes */" + fileLength.ToString(NumberFormatInfo.InvariantInfo));
        }

        private static void SendFile(string physicalPath, long offset, long length, long fileLength, HttpContext context)
        {
            try
            {
                // When not hosted on IIS, TransmitFile sends bytes in memory similar to WriteFile
                //HttpRuntime.CheckFilePermission(physicalPath);

                var checkFilePermissionMethodInfo = typeof(HttpRuntime)
                    .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Where(x => x.Name.Equals("CheckFilePermission"))
                    .Select(m => new { Method = m, Parameters = m.GetParameters() })
                    .FirstOrDefault(p =>
                        p.Parameters.Length == 1
                        && p.Parameters[0].ParameterType.Name.ToLower() == "string"
                    ).Method;

                if (checkFilePermissionMethodInfo != null)
                {
                    checkFilePermissionMethodInfo.Invoke(null, new object[1] { physicalPath });
                }

                context.Response.TransmitFile(physicalPath, offset, length);
            }
            catch (ExternalException e)
            {
                // Check for ERROR_ACCESS_DENIED and set the HTTP 
                // status such that the auth modules do their thing
                if (IsSecurityError(e.ErrorCode))
                {
                    throw new HttpException(HttpStatus.Unauthorized, "Resource access forbidden");
                }
                throw;
            }
        }

        internal static void Trace(string tagName, string message)
        {
            //System.Web.Util.Debug, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
            var type = Type.GetType("System.Web.Util.Debug, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (type != null)
            {
                var traceMethodInfo = type
                    .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Where(x => x.Name.Equals("Trace"))
                    .Select(m => new { Method = m, Parameters = m.GetParameters() })
                    .FirstOrDefault(p =>
                        p.Parameters.Length == 2
                        && p.Parameters[0].ParameterType.Name.ToLower() == "string"
                        && p.Parameters[1].ParameterType.Name.ToLower() == "string"
                    ).Method;

                if (traceMethodInfo != null)
                {
                    traceMethodInfo.Invoke(null, new object[2] { tagName, message });
                }
            }
        }
    }
}
