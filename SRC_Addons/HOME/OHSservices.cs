﻿using HttpMultipartParser;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace PSMultiServer.SRC_Addons.HOME
{
    public class OHSservices
    {
        private static string jamindecrypt = "--\r\n-- Encode Lua values as strings and decode those strings back into Lua values.\r\n-- The name is a bad pun on Bencode on which it is based.\r\n--\r\n-- Jamin.encode( table ) returns string or error\r\n-- Jamin.decode( string ) returns table or error\r\n--\r\n-- nil -> 'z;'\r\n-- bool -> 'b' ('t' | 'f') ';'\r\n-- number -> 'n' <integer-or-float> ';'\r\n-- string -> 's' [0-9]+ ':' <bytes> ';'\r\n-- vector -> 'v' <integer-or-float> ' ' <integer-or-float> ' ' <integer-or-float> ' ' <integer-or-float> ';'\r\n-- table -> 't' { key value } ';'\r\n-- key -> number | string\r\n-- value -> bool | number | string | vector | table | nil\r\n--\r\n\r\nJamin = {}\r\n\r\n\r\n-- Need a table of 'safe' charcaters that can be sent via HttpPostData and XML\r\n-- without escaping or expansion.\r\n\r\nlocal _letters = {}  -- index -> char\r\n\r\nlocal _special = {\r\n    [' '] = true,  -- Multiple spaces are concatenated by the XML parser\r\n    [\"'\"] = true,\r\n    ['\"'] = true,\r\n    ['<'] = true,\r\n    ['>'] = true,\r\n    ['#'] = true,  -- Used as an escape character in Jamin.\r\n    ['%'] = true,  -- HttpPostData will hang if this is sent.\r\n    ['&'] = true,  -- Used to escape '%' in HttpPostData mesaages.\r\n}\r\n\r\nfor i = 33, 126 do\r\n    local char = string.char(i)\r\n\r\n    if not _special[char] then\r\n        _letters[#_letters+1] = char\r\n    end\r\nend\r\n\r\nlocal _alphabet = table.concat(_letters)\r\nlocal _numLetters = #_alphabet\r\n\r\n-- _letters is of type index -> char, this is the inverse mapping from chars to\r\n-- integers.\r\nlocal _invLetters = {}\r\n\r\nfor index, letter in ipairs(_letters) do\r\n    _invLetters[letter] = index\r\nend\r\n\r\nlocal _word = {}\r\n\r\nlocal _wordMT = {\r\n    __index =\r\n        function ( tbl, val )\r\n            assert(val == math.floor(val))\r\n            assert(val >= 1)\r\n\r\n            -- A word of length val, made only from _letters can have this many\r\n            -- distinct values\r\n            local size = (_numLetters)^val\r\n            -- Smallest value a word of length val can represent.\r\n            local min = -math.floor(size * 0.5)\r\n            -- Largest value a word of length val can represent.\r\n            local max = math.floor(size * 0.5) - ((size+1) % 2)\r\n            -- Add this to change from [min..max] to [0..size-1]\r\n            local offset = -min\r\n\r\n            assert(size > 0)\r\n            assert((max - min) + 1 == size)\r\n            assert(min + offset == 0)\r\n\r\n            local data = {\r\n                size = size,\r\n                min = min,\r\n                max = max,\r\n                offset = offset,\r\n                length = val,\r\n            }\r\n\r\n            -- printf('_word[%d] = size:%s min:%s max:%s offset:%s',\r\n            --        val,\r\n            --        tostring(size),\r\n            --        tostring(min),\r\n            --        tostring(max),\r\n            --        tostring(offset))\r\n\r\n            rawset(tbl, val, data)\r\n\r\n            return data\r\n        end\r\n}\r\n\r\nsetmetatable(_word, _wordMT)\r\n\r\n\r\n\r\nlocal function encodeNil( value )\r\n    assert(type(value) == \"nil\")\r\n\r\n    return 'z;'\r\nend\r\n\r\nlocal function encodeBoolean( value )\r\n    assert(type(value) == \"boolean\")\r\n\r\n    if value then\r\n        return \"bt;\"\r\n    else\r\n        return \"bf;\"\r\n    end\r\nend\r\n\r\nlocal function encodeNumber( value )\r\n    assert(type(value) == \"number\" or Type(value) == \"BigInt\")\r\n\r\n    if type(value) == 'BigInt' then\r\n        return string.format(\"n%s;\", tostring(value))\r\n\telseif value == math.floor(value) then\r\n\t\treturn string.format(\"n%d;\", value)\r\n\telse\r\n\t\treturn string.format(\"n%s;\", tostring(value))\r\n\tend\r\nend\r\n\r\n-- Escape characters outside a safe ASCII subset. This is needed because Lua\r\n-- allows '\\0' mid-string and Home can give us utf8 strings.\r\n--\r\n-- The characters in the range [32..127] are considered safe. Space,\r\n-- alphanumeric and punctuation characters are in this range.\r\n-- An escape is in one of two forms:\r\n--   '#xxx' where xxx is the 0 extended, decimal code for the character.\r\n--   '##' represents '#' in an unescaped string.\r\n--\r\nlocal function escapeString( str )\r\n    local partial = str:gsub(\"#\", \"##\")\r\n\r\n    local function expandUnsafeCharacters( str )\r\n        local chars = {}\r\n        for idx = 1, #str do\r\n            table.insert(chars, string.format(\"#%03d\", str:byte(idx)))\r\n        end\r\n        return table.concat(chars)\r\n    end\r\n\r\n    return partial:gsub(\"([^%w%p ]+)\",  expandUnsafeCharacters)\r\nend\r\n\r\nlocal function encodeString( value )\r\n    local escaped = escapeString(value)\r\n\r\n    return string.format(\"s%d:%s;\", escaped:len(), escaped)\r\nend\r\n\r\nlocal function encodeVector( value )\r\n    return string.format(\"v%f %f %f %f;\", value:X(), value:Y(), value:Z(), value:W())\r\nend\r\n\r\nfunction iskey( value )\r\n    local t = type(value)\r\n\r\n    return t == \"number\" or t == \"string\"\r\nend\r\n\r\n-- The function below can create a lot of garbage so care has to be taken\r\n-- to merge intermediate results and call the collector.\r\n-- NB: Need to improve this function:\r\n--     - The length of the strings needs to be taken into account.\r\n--     - The garbage collector can be called quite often.\r\nlocal function encodeTable( value, isCoroutine )\r\n    local strings = { 't' }\r\n    local maxlen = 100\r\n\r\n    for k, v in pairs(value) do\r\n        assert(iskey(k), string.format(\"%s can't be used as a key only strings and numbers allowed\", tostring(k)))\r\n\r\n        local kstr\r\n        if isCoroutine then\r\n            coroutine.yield(false)\r\n            kstr = Jamin.coroutineEncode(k)\r\n        else\r\n            kstr = Jamin.encode(k)\r\n        end\r\n\r\n        local vstr\r\n        if isCoroutine then\r\n            coroutine.yield(false)\r\n            vstr = Jamin.coroutineEncode(v)\r\n        else\r\n            vstr = Jamin.encode(v)\r\n        end\r\n\r\n        strings[#strings+1] = kstr\r\n        strings[#strings+1] = vstr\r\n\r\n        if #strings > maxlen then\r\n            strings = { table.concat(strings) }\r\n\r\n            collectgarbage('collect')\r\n        end\r\n    end\r\n\r\n    strings[#strings+1] = ';'\r\n\r\n    local result = table.concat(strings)\r\n\r\n    -- collectgarbage('collect')\r\n\r\n    return result\r\nend\r\n\r\nlocal function encodeFunction( value )\r\n    return value()\r\nend\r\n\r\nlocal _minIntegerArrayElement = -(2^24)\r\nlocal _maxIntegerArrayElement = 2^24\r\n\r\nfunction _isValidIntegerArrayElement( value )\r\n    if value == math.floor(value) then\r\n        if _minIntegerArrayElement <= value and value <= _maxIntegerArrayElement then\r\n            return true\r\n        end\r\n    end\r\n\r\n    return false\r\nend\r\n\r\nfunction calcIntegerArrayWordLength( array )\r\n    local result = 0\r\n\r\n    if #array > 0 then\r\n        local min = _maxIntegerArrayElement\r\n        local max = _minIntegerArrayElement\r\n\r\n        for _, v in ipairs(array) do\r\n            assert(_isValidIntegerArrayElement(v))\r\n\r\n            min = math.min(min, v)\r\n            max = math.max(max, v)\r\n        end\r\n\r\n        local normedRange = math.max(math.abs(min), math.abs(max)) * 2\r\n\r\n        result = math.ceil(math.log(normedRange) / math.log(_numLetters))\r\n\r\n        -- printf('[%s..%s] %s -> %d', tostring(min), tostring(max), tostring(normedRange), result)\r\n    end\r\n\r\n    return result\r\nend\r\n\r\nfunction Jamin.encodeIntegerArrayViaIterator( iterator, wordlen )\r\n    local word = _word[wordlen]\r\n    local results = { '' }  -- don't know how long it is yet.\r\n    local count = 0\r\n\r\n    for element in iterator do\r\n        count = count + 1\r\n        -- [word.min..word.max] -> [0..word.size-1]\r\n        local normed = element + word.offset\r\n\r\n        assert(0 <= normed and normed < word.size)\r\n\r\n        -- Little Endian\r\n        for i = 0, wordlen - 1 do\r\n            local index = math.floor(normed / (_numLetters)^i) % _numLetters\r\n            -- printf('val:%d normed:%d i:%d index:%d', val, normed, i, index)\r\n            results[#results+1] = _letters[index+1]\r\n        end\r\n    end\r\n\r\n    results[1] = string.format('i%d,%d:', wordlen, count)\r\n\r\n    assert(#results == (count * wordlen) + 1)\r\n\r\n    results[#results+1] = ';'\r\n\r\n    return table.concat(results)\r\nend\r\n\r\nfunction Jamin.encodeIntegerArray( value, wordlen )\r\n    assert(type(value) == 'table')\r\n\r\n    wordlen = wordlen or calcIntegerArrayWordLength(value)\r\n\r\n    local word = _word[wordlen]\r\n    local results = { string.format('i%d,%d:', wordlen, #value) }\r\n\r\n    for _, element in ipairs(value) do\r\n        -- [word.min..word.max] -> [0..word.size-1]\r\n        local normed = element + word.offset\r\n\r\n        assert(0 <= normed and normed < word.size)\r\n\r\n        -- Little Endian\r\n        for i = 0, wordlen - 1 do\r\n            local index = math.floor(normed / (_numLetters)^i) % _numLetters\r\n            -- printf('val:%d normed:%d i:%d index:%d', val, normed, i, index)\r\n            results[#results+1] = _letters[index+1]\r\n        end\r\n    end\r\n\r\n    assert(#results == (#value * wordlen) + 1)\r\n\r\n    results[#results+1] = ';'\r\n\r\n    return table.concat(results)\r\nend\r\n\r\n\r\nlocal encoders = {\r\n    [\"nil\"] = encodeNil,\r\n    [\"boolean\"] = encodeBoolean,\r\n    [\"number\"] = encodeNumber,\r\n    [\"BigInt\"] = encodeNumber,\r\n    [\"string\"] = encodeString,\r\n    [\"Vector4\"] = encodeVector,\r\n    [\"table\"] = encodeTable,\r\n    [\"function\"] = encodeFunction,\r\n}\r\n\r\n-- Type() is from the Home HDK for use on HDK values.\r\nlocal function _type( x )\r\n    local result = type(x)\r\n\r\n    if result == \"userdata\" then\r\n        result = Type(x)\r\n    end\r\n\r\n    return result\r\nend\r\n\r\nlocal function encodeError( value )\r\n    error(string.format(\"can't encode %s of type %s\", tostring(value), _type(value)))\r\nend\r\n\r\nfunction Jamin.encode( value )\r\n    local encoder = encoders[_type(value)] or encodeError\r\n\r\n    return encoder(value)\r\nend\r\n\r\nfunction Jamin.coroutineEncode( value )\r\n    local encoder = encoders[_type(value)] or encodeError\r\n\r\n    return encoder(value, true)\r\nend\r\n\r\n-------------------------------------------------------------------------------\r\n\r\n\r\n-- str:sub(pos, pos) == \"z\"\r\nlocal function decodeNil( str, pos )\r\n    local start, finish = str:find(\"z;\", pos)\r\n\r\n    if start == pos and finish == pos + 1 then\r\n        return nil, pos + 2\r\n    else\r\n        error(string.format(\"%s is not a valid nil at pos %d\", str:sub(pos, pos+2), pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"b\"\r\nlocal function decodeBoolean( str, pos )\r\n    local encoded, finish = str:match(\"b([tf]);()\", pos)\r\n\r\n    if encoded then\r\n        return encoded == \"t\", pos + 3\r\n    else\r\n        error(string.format(\"%s is not a valid boolean at pos %d\", str:sub(pos, pos+2), pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"n\"\r\nlocal function decodeNumber( str, pos )\r\n    local encoded, finish = str:match(\"n([^;]+);()\", pos)\r\n\r\n    local decoded = tonumber(encoded)\r\n    if decoded > 2^24 or decoded < -2^24 then\r\n        decoded = BigInt.Create('64', encoded)\r\n    end\r\n\r\n    if decoded then\r\n        return decoded, finish\r\n    else\r\n        error(string.format(\"'%s' is not a valid number at pos %d\", encoded, pos))\r\n    end\r\nend\r\n\r\nlocal function _unescapeUnsafeChracters( str )\r\n    return string.char(tonumber(str))\r\nend\r\n\r\n\r\n-- See escapeString() above for more details\r\nlocal function unescapeString( str )\r\n    local partial = str:gsub(\"##\", \"#\")\r\n\r\n    return partial:gsub(\"#(%d%d%d)\", _unescapeUnsafeChracters)\r\nend\r\n\r\n-- str:sub(pos, pos) == \"s\"\r\nlocal function decodeString( str, pos )\r\n    local encoded = str:match(\"s([%d]+):\", pos)\r\n    local length = tonumber(encoded)\r\n\r\n    if length then\r\n        local start = pos + 2 + encoded:len()\r\n        local finish = start + (length-1)\r\n\r\n        if str:sub(finish+1, finish+1) == \";\" then\r\n            local escaped = str:sub(start, finish)\r\n            local unescaped = unescapeString(escaped)\r\n            return unescaped, finish + 2\r\n        else\r\n            error(string.format(\"couldn't find terminating ';' for string at pos %d - context: %q\", pos, str:sub(pos, finish+1)))\r\n        end\r\n    else\r\n        error(string.format(\"couldn't find length of string at pos %d\", pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"v\"\r\nlocal function decodeVector( str, pos )\r\n    local encX, encY, encZ, encW, finish = str:match(\"v([^ ;]+) ([^ ;]+) ([^ ;]+) ([^ ;]+);()\", pos)\r\n    local x, y, z, w = tonumber(encX), tonumber(encY), tonumber(encZ), tonumber(encW)\r\n\r\n    if x and y and z and w then\r\n        return Vector4.Create(x, y, z, w), finish\r\n    else\r\n        error(string.format(\"couldn't parse vector at pos %d\", pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"t\"\r\nlocal function decodeTable( str, pos )\r\n    local result = {}\r\n    local key, cursor = nil, pos + 1\r\n\r\n    while str:sub(cursor, cursor) ~= \";\" do\r\n        key, cursor = Jamin.decode(str, cursor)\r\n        assert(iskey(key), string.format(\"%s can't be used as a key only strings and numbers allowed\", tostring(key)))\r\n\r\n        value, cursor = Jamin.decode(str, cursor)\r\n\r\n        result[key] = value\r\n    end\r\n\r\n    return result, cursor+1\r\nend\r\n\r\nfunction Jamin.decodeIntegerArray( str, pos )\r\n    pos = pos or 1\r\n\r\n    local encWordlen, encNumElements = str:match(\"i([%d]+),([%d]+):\", pos)\r\n    local wordlen = tonumber(encWordlen)\r\n    local numElements = tonumber(encNumElements)\r\n\r\n    if wordlen and numElements then\r\n        local word = _word[wordlen]\r\n        local start = pos + 3 + encWordlen:len() + encNumElements:len()\r\n        local finish = start + (wordlen * numElements) - 1\r\n\r\n        if str:sub(finish+1, finish+1) == \";\" then\r\n            local encoded = str:sub(start, finish)\r\n\r\n            -- printf('encoded: %s', encoded)\r\n\r\n            local result= {}\r\n            local count = 0\r\n            local normed = 0\r\n\r\n            -- Should be more memory efficient matching each char seperately as\r\n            -- they're already in the _letters and _invLetters tables.\r\n            for encDigit in encoded:gmatch('.') do\r\n                local wordIndex = count % wordlen\r\n\r\n                local digit = (_invLetters[encDigit] - 1) * ((_numLetters)^wordIndex)\r\n\r\n                -- print('encdigit:', encdigit, 'wordIndex:', wordIndex, 'digit:', digit)\r\n                normed = normed + digit\r\n\r\n                -- Is this the last letter of a word\r\n                if wordIndex == wordlen - 1 then\r\n                    result[#result+1] = normed - word.offset\r\n                    normed = 0\r\n                end\r\n\r\n                count = count + 1\r\n            end\r\n\r\n            return result, finish + 2\r\n        else\r\n            error(string.format(\"couldn't find terminating ';' for integer-array at pos %d, found %q\", pos, str:sub(finish+1, finish+1)))\r\n        end\r\n    else\r\n        error(string.format(\"couldn't find one or both of wordlen and #elements in integer-array at pos %d\", pos))\r\n    end\r\nend\r\n\r\nlocal decoders = {\r\n    [\"z\"] = decodeNil,\r\n    [\"b\"] = decodeBoolean,\r\n    [\"n\"] = decodeNumber,\r\n    [\"s\"] = decodeString,\r\n    [\"v\"] = decodeVector,\r\n    [\"t\"] = decodeTable,\r\n    [\"i\"] = decodeIntegerArray,\r\n}\r\n\r\nlocal function decodeError( str, pos )\r\n    error(string.format(\"no decoder for %q at pos %d\", str:sub(pos, pos), pos))\r\nend\r\n\r\nfunction Jamin.decode( str, pos )\r\n    pos = pos or 1\r\n\r\n    local decoder = decoders[str:sub(pos, pos)] or decodeError\r\n\r\n    return decoder(str, pos)\r\nend\r\n\r\n-------------------- Custom code for PSMultiServer\r\n\r\n--local function tableToString(table, indent)\r\n--   indent = indent or 0\r\n--    local spacing = string.rep(\"  \", indent)\r\n--    local result = \"{\\n\"\r\n--    local first = true\r\n--\r\n--    for k, v in pairs(table) do\r\n--        if type(k) == \"number\" then\r\n--            k = \"[\" .. tostring(k) .. \"]\"\r\n--        else\r\n--            k = '[\"' .. tostring(k) .. '\"]'\r\n--        end\r\n--\r\n--        if type(v) == \"table\" then\r\n--            result = result .. (first and \"\" or \",\\n\") .. spacing .. k .. \" = \" .. tableToString(v, indent + 1)\r\n--        else\r\n--            v = type(v) == \"string\" and '\"' .. v .. '\"' or tostring(v)\r\n--            result = result .. (first and \"\" or \",\\n\") .. spacing .. k .. \" = \" .. v\r\n--        end\r\n--\r\n--        first = false\r\n--    end\r\n--\r\n--    result = result .. \"\\n\" .. string.rep(\"  \", indent) .. \"}\"\r\n--\r\n--    return result\r\n--end\r\n\r\n-----------------------------------------------------------------------------\r\n-- JSON4Lua: JSON encoding / decoding / traversing support for the Lua language.\r\n-- json Module.\r\n-- Authors: Craig Mason-Jones, Egor Skriptunoff\r\n\r\n-- Version: 1.2.1\r\n-- 2017-05-10\r\n\r\n-- This module is released under the MIT License (MIT).\r\n-- Please see LICENCE.txt for details:\r\n-- https://github.com/craigmj/json4lua/blob/master/doc/LICENCE.txt\r\n--\r\n-- USAGE:\r\n-- This module exposes three functions:\r\n--   json.encode(obj)\r\n--     Accepts Lua value (table/string/boolean/number/nil/json.null/json.empty) and returns JSON string.\r\n--   json.decode(s)\r\n--     Accepts JSON (as string or as loader function) and returns Lua object.\r\n--   json.traverse(s, callback)\r\n--     Accepts JSON (as string or as loader function) and user-supplied callback function, returns nothing\r\n--     Traverses the JSON, sends each item to callback function, no memory-consuming Lua objects are being created.\r\n\r\n--\r\n-- REQUIREMENTS:\r\n--   Lua 5.1, Lua 5.2, Lua 5.3 or LuaJIT\r\n--\r\n-- CHANGELOG\r\n--   1.2.1   Now you can partially decode JSON while traversing it (callback function should return true).\r\n--   1.2.0   Some improvements made to be able to use this module on RAM restricted devices:\r\n--             To read large JSONs, you can now provide \"loader function\" instead of preloading whole JSON as Lua string.\r\n--             Added json.traverse() to traverse JSON using callback function (without creating arrays/objects in Lua).\r\n--             Now, instead of decoding whole JSON, you can decode its arbitrary element (e.g, array or object)\r\n--                by specifying the position where this element starts.\r\n--                In order to do that, at first you have to traverse JSON to get all positions you need.\r\n--           Most of the code rewritten to improve performance.\r\n--           Decoder now understands extended syntax beyond strict JSON standard:\r\n--             In arrays:\r\n--                trailing comma is ignored:            [1,2,3,]     -> [1,2,3]\r\n--                missing values are nulls:             [,,42,,]     -> [null,null,42,null]\r\n--             In objects:\r\n--                missing values are ignored:           {,,\"a\":42,,} -> {\"a\":42}\r\n--                unquoted identifiers are valid keys:  {a$b_5:42}   -> {\"a$b_5\":42}\r\n--           Encoder now accepts both 0-based and 1-based Lua arrays (but decoder always converts JSON arrays to 1-based Lua arrays).\r\n--           Some minor bugs fixed.\r\n--   1.1.0   Modifications made by Egor Skriptunoff, based on version 1.0.0 taken from\r\n--              https://github.com/craigmj/json4lua/blob/40fb13b0ec4a70e36f88812848511c5867bed857/json/json.lua.\r\n--           Added Lua 5.2 and Lua 5.3 compatibility.\r\n--           Removed Lua 5.0 compatibility.\r\n--           Introduced json.empty (Lua counterpart for empty JSON object)\r\n--           Bugs fixed:\r\n--              Attempt to encode Lua table {[10^9]=0} raises an out-of-memory error.\r\n--              Zero bytes '\\0' in Lua strings are not escaped by encoder.\r\n--              JSON numbers with capital \"E\" (as in 1E+100) are not accepted by decoder.\r\n--              All nulls in a JSON arrays are skipped by decoder, sparse arrays could not be loaded correctly.\r\n--              UTF-16 surrogate pairs in JSON strings are not recognised by decoder.\r\n--   1.0.0   Merged Amr Hassan's changes\r\n--   0.9.30  Changed to MIT Licence.\r\n--   0.9.20  Introduction of local Lua functions for private functions (removed _ function prefix).\r\n--           Fixed Lua 5.1 compatibility issues.\r\n--           Introduced json.null to have null values in associative arrays.\r\n--           json.encode() performance improvement (more than 50%) through table_concat rather than ..\r\n--           Introduced decode ability to ignore /**/ comments in the JSON string.\r\n--   0.9.10  Fix to array encoding / decoding to correctly manage nil/null values in arrays.\r\n--   0.9.00  First release\r\n--\r\n-----------------------------------------------------------------------------\r\n\r\n-----------------------------------------------------------------------------\r\n-- Module declaration\r\n-----------------------------------------------------------------------------\r\nlocal json = {}\r\n\r\ndo\r\n   -----------------------------------------------------------------------------\r\n   -- Imports and dependencies\r\n   -----------------------------------------------------------------------------\r\n   local math, string, table = require'math', require'string', require'table'\r\n   local math_floor, math_max, math_type = math.floor, math.max, math.type or function() end\r\n   local string_char, string_sub, string_find, string_match, string_gsub, string_format\r\n      = string.char, string.sub, string.find, string.match, string.gsub, string.format\r\n   local table_insert, table_remove, table_concat = table.insert, table.remove, table.concat\r\n   local type, tostring, pairs, assert, error = type, tostring, pairs, assert, error\r\n   local loadstring = loadstring or load\r\n\r\n   -----------------------------------------------------------------------------\r\n   -- Public functions\r\n   -----------------------------------------------------------------------------\r\n   -- function  json.encode(obj)       encodes Lua value to JSON, returns JSON as string.\r\n   -- function  json.decode(s, pos)    decodes JSON, returns the decoded result as Lua value (may be very memory-consuming).\r\n\r\n   --    Both functions json.encode() and json.decode() work with \"special\" Lua values json.null and json.empty\r\n   --       special Lua value  json.null    =  JSON value  null\r\n   --       special Lua value  json.empty   =  JSON value  {}     (empty JSON object)\r\n   --       regular Lua empty table         =  JSON value  []     (empty JSON array)\r\n\r\n   --    Empty JSON objects and JSON nulls require special handling upon sending (encoding).\r\n   --       Please make sure that you send empty JSON objects as json.empty (instead of empty Lua table).\r\n   --       Empty Lua tables will be encoded as empty JSON arrays, not as empty JSON objects!\r\n   --          json.encode( {empt_obj = json.empty, empt_arr = {}} )   -->   {\"empt_obj\":{},\"empt_arr\":[]}\r\n   --       Also make sure you send JSON nulls as json.null (instead of nil).\r\n   --          json.encode( {correct = json.null, incorrect = nil} )   -->   {\"correct\":null}\r\n\r\n   --    Empty JSON objects and JSON nulls require special handling upon receiving (decoding).\r\n   --       After receiving the result of decoding, every Lua table returned (including nested tables) should firstly\r\n   --       be compared with special Lua values json.empty/json.null prior to making operations on these values.\r\n   --       If you don't need to distinguish between empty JSON objects and empty JSON arrays,\r\n   --       json.empty may be replaced with newly created regular empty Lua table.\r\n   --          v = (v == json.empty) and {} or v\r\n   --       If you don't need special handling of JSON nulls, you may replace json.null with nil to make them disappear.\r\n   --          if v == json.null then v = nil end\r\n\r\n   -- Function  json.traverse(s, callback, pos)  traverses JSON using user-supplied callback function, returns nothing.\r\n   --    Traverse is useful to reduce memory usage: no memory-consuming objects are being created in Lua while traversing.\r\n   --    Each item found inside JSON will be sent to callback function passing the following arguments:\r\n   --    (path, json_type, value, pos, pos_last)\r\n   --       path      is array of nested JSON identifiers/indices, \"path\" is empty for root JSON element\r\n   --       json_type is one of \"null\"/\"boolean\"/\"number\"/\"string\"/\"array\"/\"object\"\r\n   --       value     is defined when json_type is \"null\"/\"boolean\"/\"number\"/\"string\", value == nil for \"object\"/\"array\"\r\n   --       pos       is 1-based index of first character of current JSON element\r\n   --       pos_last  is 1-based index of last character of current JSON element (defined only when \"value\" ~= nil)\r\n   -- \"path\" table reference is the same on each callback invocation, but its content differs every time.\r\n   --    Do not modify \"path\" array inside your callback function, use it as read-only.\r\n   --    Do not save reference to \"path\" for future use (create shallow table copy instead).\r\n   -- callback function should return a value, when it is invoked with argument \"value\" == nil\r\n   --    a truthy value means user wants to decode this JSON object/array and create its Lua counterpart (this may be memory-consuming)\r\n   --    a falsy value (or no value returned) means user wants to traverse through this JSON object/array\r\n   --    (returned value is ignored when callback function is invoked with value ~= nil)\r\n\r\n   -- Traverse examples:\r\n\r\n   --    json.traverse([[ 42 ]], callback)\r\n   --    will invoke callback 1 time:\r\n   --                 path        json_type  value           pos  pos_last\r\n   --                 ----------  ---------  --------------  ---  --------\r\n   --       callback( {},         \"number\",  42,             2,   3   )\r\n   --\r\n   --    json.traverse([[ {\"a\":true, \"b\":null, \"c\":[\"one\",\"two\"], \"d\":{ \"e\":{}, \"f\":[] } } ]], callback)\r\n   --    will invoke callback 9 times:\r\n   --                 path        json_type  value           pos  pos_last\r\n   --                 ----------  ---------  --------------  ---  --------\r\n   --       callback( {},         \"object\",  nil,            2,   nil )\r\n   --       callback( {\"a\"},      \"boolean\", true,           7,   10  )\r\n   --       callback( {\"b\"},      \"null\",    json.null,      17,  20  )   -- special Lua value for JSON null\r\n   --       callback( {\"c\"},      \"array\",   nil,            27,  nil )\r\n   --       callback( {\"c\", 1},   \"string\",  \"one\",          28,  32  )\r\n   --       callback( {\"c\", 2},   \"string\",  \"two\",          34,  38  )\r\n   --       callback( {\"d\"},      \"object\",  nil,            46,  nil )\r\n   --       callback( {\"d\", \"e\"}, \"object\",  nil,            52,  nil )\r\n   --       callback( {\"d\", \"f\"}, \"array\",   nil,            60,  nil )\r\n   --\r\n   --    json.traverse([[ {\"a\":true, \"b\":null, \"c\":[\"one\",\"two\"], \"d\":{ \"e\":{}, \"f\":[] } } ]], callback)\r\n   --    will invoke callback 9 times if callback returns true when invoked for array \"c\" and object \"e\":\r\n   --                 path        json_type  value           pos  pos_last\r\n   --                 ----------  ---------  --------------  ---  --------\r\n   --       callback( {},         \"object\",  nil,            2,   nil )\r\n   --       callback( {\"a\"},      \"boolean\", true,           7,   10  )\r\n   --       callback( {\"b\"},      \"null\",    json.null,      17,  20  )\r\n   --       callback( {\"c\"},      \"array\",   nil,            27,  nil )  -- this callback returned true (user wants to decode this array)\r\n   --       callback( {\"c\"},      \"array\",   {\"one\", \"two\"}, 27,  39  )  -- the next invocation brings the result of decoding\r\n   --       callback( {\"d\"},      \"object\",  nil,            46,  nil )\r\n   --       callback( {\"d\", \"e\"}, \"object\",  nil,            52,  nil )  -- this callback returned true (user wants to decode this object)\r\n   --       callback( {\"d\", \"e\"}, \"object\",  json.empty,     52,  53  )  -- the next invocation brings the result of decoding (special Lua value for empty JSON object)\r\n   --       callback( {\"d\", \"f\"}, \"array\",   nil,            60,  nil )\r\n\r\n\r\n   -- Both decoder functions json.decode(s) and json.traverse(s, callback) can accept JSON (argument s)\r\n   --    as a \"loader function\" instead of a string.\r\n   --    This function will be called repeatedly to return next parts (substrings) of JSON.\r\n   --    An empty string, nil, or no value returned from \"loader function\" means the end of JSON.\r\n   --    This may be useful for low-memory devices or for traversing huge JSON files.\r\n\r\n\r\n   --- The json.null table allows one to specify a null value in an associative array (which is otherwise\r\n   -- discarded if you set the value with 'nil' in Lua. Simply set t = { first=json.null }\r\n   local null = {\"This Lua table is used to designate JSON null value, compare your values with json.null to determine JSON nulls\"}\r\n   json.null = setmetatable(null, {\r\n      __tostring = function() return 'null' end\r\n   })\r\n\r\n   --- The json.empty table allows one to specify an empty JSON object.\r\n   -- To encode empty JSON array use usual empty Lua table.\r\n   -- Example: t = { empty_object=json.empty, empty_array={} }\r\n   local empty = {}\r\n   json.empty = setmetatable(empty, {\r\n      __tostring = function() return '{}' end,\r\n      __newindex = function() error(\"json.empty is an read-only Lua table\", 2) end\r\n   })\r\n\r\n   -----------------------------------------------------------------------------\r\n   -- Private functions\r\n   -----------------------------------------------------------------------------\r\n   local decode\r\n   local decode_scanArray\r\n   local decode_scanConstant\r\n   local decode_scanNumber\r\n   local decode_scanObject\r\n   local decode_scanString\r\n   local decode_scanIdentifier\r\n   local decode_scanWhitespace\r\n   local encodeString\r\n   local isArray\r\n   local isEncodable\r\n   local isConvertibleToString\r\n   local isRegularNumber\r\n\r\n   -----------------------------------------------------------------------------\r\n   -- PUBLIC FUNCTIONS\r\n   -----------------------------------------------------------------------------\r\n   --- Encodes an arbitrary Lua object / variable.\r\n   -- @param   obj     Lua value (table/string/boolean/number/nil/json.null/json.empty) to be JSON-encoded.\r\n   -- @return  string  String containing the JSON encoding.\r\n   function json.encode(obj)\r\n      -- Handle nil and null values\r\n      if obj == nil or obj == null then\r\n         return 'null'\r\n      end\r\n\r\n      -- Handle empty JSON object\r\n      if obj == empty then\r\n         return '{}'\r\n      end\r\n\r\n      local obj_type = type(obj)\r\n\r\n      -- Handle strings\r\n      if obj_type == 'string' then\r\n         return '\"'..encodeString(obj)..'\"'\r\n      end\r\n\r\n      -- Handle booleans\r\n      if obj_type == 'boolean' then\r\n         return tostring(obj)\r\n      end\r\n\r\n      -- Handle numbers\r\n      if obj_type == 'number' then\r\n         assert(isRegularNumber(obj), 'numeric values Inf and NaN are unsupported')\r\n         return math_type(obj) == 'integer' and tostring(obj) or string_format('%.17g', obj)\r\n      end\r\n\r\n      -- Handle tables\r\n      if obj_type == 'table' then\r\n         local rval = {}\r\n         -- Consider arrays separately\r\n         local bArray, maxCount = isArray(obj)\r\n         if bArray then\r\n            for i = obj[0] ~= nil and 0 or 1, maxCount do\r\n               table_insert(rval, json.encode(obj[i]))\r\n            end\r\n         else  -- An object, not an array\r\n            for i, j in pairs(obj) do\r\n               if isConvertibleToString(i) and isEncodable(j) then\r\n                  table_insert(rval, '\"'..encodeString(i)..'\":'..json.encode(j))\r\n               end\r\n            end\r\n         end\r\n         if bArray then\r\n            return '['..table_concat(rval, ',')..']'\r\n         else\r\n            return '{'..table_concat(rval, ',')..'}'\r\n         end\r\n      end\r\n\r\n      error('Unable to JSON-encode Lua value of unsupported type \"'..obj_type..'\": '..tostring(obj))\r\n   end\r\n\r\n   local function create_state(s)\r\n      -- Argument s may be \"whole JSON string\" or \"JSON loader function\"\r\n      -- Returns \"state\" object which holds current state of reading long JSON:\r\n      --    part = current part (substring of long JSON string)\r\n      --    disp = number of bytes before current part inside long JSON\r\n      --    more = function to load next substring (more == nil if all substrings are already read)\r\n      local state = {disp = 0}\r\n      if type(s) == \"string\" then\r\n         -- s is whole JSON string\r\n         state.part = s\r\n      else\r\n         -- s is loader function\r\n         state.part = \"\"\r\n         state.more = s\r\n      end\r\n      return state\r\n   end\r\n\r\n   --- Decodes a JSON string and returns the decoded value as a Lua data structure / value.\r\n   -- @param   s           The string to scan (or \"loader function\" for getting next substring).\r\n   -- @param   pos         (optional) The position inside s to start scan, default = 1.\r\n   -- @return  Lua object  The object that was scanned, as a Lua table / string / number / boolean / json.null / json.empty.\r\n   function json.decode(s, pos)\r\n      return (decode(create_state(s), pos or 1))\r\n   end\r\n\r\n   --- Traverses a JSON string, sends everything to user-supplied callback function, returns nothing\r\n   -- @param   s           The string to scan (or \"loader function\" for getting next substring).\r\n   -- @param   callback    The user-supplied callback function which accepts arguments (path, json_type, value, pos, pos_last).\r\n   -- @param   pos         (optional) The position inside s to start scan, default = 1.\r\n   function json.traverse(s, callback, pos)\r\n      decode(create_state(s), pos or 1, {path = {}, callback = callback})\r\n   end\r\n\r\n   local function read_ahead(state, startPos)\r\n      -- Make sure there are at least 32 bytes read ahead\r\n      local endPos = startPos + 31\r\n      local part = state.part  -- current part (substring of \"whole JSON\" string)\r\n      local disp = state.disp  -- number of bytes before current part inside \"whole JSON\" string\r\n      local more = state.more  -- function to load next substring\r\n      assert(startPos > disp)\r\n      while more and disp + #part < endPos do\r\n         --  (disp + 1) ... (disp + #part)  -  we already have this segment now\r\n         --  startPos   ... endPos          -  we need to have this segment\r\n         local next_substr = more()\r\n         if not next_substr or next_substr == \"\" then\r\n            more = nil\r\n         else\r\n            disp, part = disp + #part, string_sub(part, startPos - disp)\r\n            disp, part = disp - #part, part..next_substr\r\n         end\r\n      end\r\n      state.disp, state.part, state.more = disp, part, more\r\n   end\r\n\r\n   local function get_word(state, startPos, length)\r\n      -- 1 <= length <= 32\r\n      if state.more then read_ahead(state, startPos) end\r\n      local idx = startPos - state.disp\r\n      return string_sub(state.part, idx, idx + length - 1)\r\n   end\r\n\r\n   local function skip_until_word(state, startPos, word)\r\n      -- #word < 30\r\n      -- returns position after that word (nil if not found)\r\n      repeat\r\n         if state.more then read_ahead(state, startPos) end\r\n         local part, disp = state.part, state.disp\r\n         local b, e = string_find(part, word, startPos - disp, true)\r\n         if b then\r\n            return disp + e + 1\r\n         end\r\n         startPos = disp + #part + 2 - #word\r\n      until not state.more\r\n   end\r\n\r\n   local function match_with_pattern(state, startPos, pattern, operation)\r\n      -- pattern must be\r\n      --    \"^[some set of chars]+\"\r\n      -- returns\r\n      --    matched_string, endPos   for operation \"read\"   (matched_string == \"\" if no match found)\r\n      --    endPos                   for operation \"skip\"\r\n      if operation == \"read\" then\r\n         local t = {}\r\n         repeat\r\n            if state.more then read_ahead(state, startPos) end\r\n            local part, disp = state.part, state.disp\r\n            local str = string_match(part, pattern, startPos - disp)\r\n            if str then\r\n               table_insert(t, str)\r\n               startPos = startPos + #str\r\n            end\r\n         until not str or startPos <= disp + #part\r\n         return table_concat(t), startPos\r\n      elseif operation == \"skip\" then\r\n         repeat\r\n            if state.more then read_ahead(state, startPos) end\r\n            local part, disp = state.part, state.disp\r\n            local b, e = string_find(part, pattern, startPos - disp)\r\n            if b then\r\n               startPos = startPos + e - b + 1\r\n            end\r\n         until not b or startPos <= disp + #part\r\n         return startPos\r\n      else\r\n         error(\"Wrong operation name\")\r\n      end\r\n   end\r\n\r\n   --- Decodes a JSON string and returns the decoded value as a Lua data structure / value.\r\n   -- @param   state             The state of JSON reader.\r\n   -- @param   startPos          Starting position where the JSON string is located.\r\n   -- @param   traverse          (optional) table with fields \"path\" and \"callback\" for traversing JSON.\r\n   -- @param   decode_key        (optional) boolean flag for decoding key inside JSON object.\r\n   -- @return  Lua_object,int    The object that was scanned, as a Lua table / string / number / boolean / json.null / json.empty,\r\n   --                            and the position of the first character after the scanned JSON object.\r\n   function decode(state, startPos, traverse, decode_key)\r\n      local curChar, value, nextPos\r\n      startPos, curChar = decode_scanWhitespace(state, startPos)\r\n      if curChar == '{' and not decode_key then\r\n         -- Object\r\n         if traverse and traverse.callback(traverse.path, \"object\", nil, startPos, nil) then\r\n            -- user wants to decode this JSON object (and get it as Lua value) while traversing\r\n            local object, endPos = decode_scanObject(state, startPos)\r\n            traverse.callback(traverse.path, \"object\", object, startPos, endPos - 1)\r\n            return false, endPos\r\n         end\r\n         return decode_scanObject(state, startPos, traverse)\r\n      elseif curChar == '[' and not decode_key then\r\n         -- Array\r\n         if traverse and traverse.callback(traverse.path, \"array\", nil, startPos, nil) then\r\n            -- user wants to decode this JSON array (and get it as Lua value) while traversing\r\n            local array, endPos = decode_scanArray(state, startPos)\r\n            traverse.callback(traverse.path, \"array\", array, startPos, endPos - 1)\r\n            return false, endPos\r\n         end\r\n         return decode_scanArray(state, startPos, traverse)\r\n      elseif curChar == '\"' then\r\n         -- String\r\n         value, nextPos = decode_scanString(state, startPos)\r\n         if traverse then\r\n            traverse.callback(traverse.path, \"string\", value, startPos, nextPos - 1)\r\n         end\r\n      elseif decode_key then\r\n         -- Unquoted string as key name\r\n         return decode_scanIdentifier(state, startPos)\r\n      elseif string_find(curChar, \"^[%d%-]\") then\r\n         -- Number\r\n         value, nextPos = decode_scanNumber(state, startPos)\r\n         if traverse then\r\n            traverse.callback(traverse.path, \"number\", value, startPos, nextPos - 1)\r\n         end\r\n      else\r\n         -- Otherwise, it must be a constant\r\n         value, nextPos = decode_scanConstant(state, startPos)\r\n         if traverse then\r\n            traverse.callback(traverse.path, value == null and \"null\" or \"boolean\", value, startPos, nextPos - 1)\r\n         end\r\n      end\r\n      return value, nextPos\r\n   end\r\n\r\n   -----------------------------------------------------------------------------\r\n   -- Internal, PRIVATE functions.\r\n   -- Following a Python-like convention, I have prefixed all these 'PRIVATE'\r\n   -- functions with an underscore.\r\n   -----------------------------------------------------------------------------\r\n\r\n   --- Scans an array from JSON into a Lua object\r\n   -- startPos begins at the start of the array.\r\n   -- Returns the array and the next starting position\r\n   -- @param   state       The state of JSON reader.\r\n   -- @param   startPos    The starting position for the scan.\r\n   -- @param   traverse    (optional) table with fields \"path\" and \"callback\" for traversing JSON.\r\n   -- @return  table,int   The scanned array as a table, and the position of the next character to scan.\r\n   function decode_scanArray(state, startPos, traverse)\r\n      local array = not traverse and {}  -- The return value\r\n      local elem_index, elem_ready, object = 1\r\n      startPos = startPos + 1\r\n      -- Infinite loop for array elements\r\n      while true do\r\n         repeat\r\n            local curChar\r\n            startPos, curChar = decode_scanWhitespace(state, startPos)\r\n            if curChar == ']' then\r\n               return array, startPos + 1\r\n            elseif curChar == ',' then\r\n               if not elem_ready then\r\n                  -- missing value in JSON array\r\n                  if traverse then\r\n                     table_insert(traverse.path, elem_index)\r\n                     traverse.callback(traverse.path, \"null\", null, startPos, startPos - 1)  -- empty substring: pos_last = pos - 1\r\n                     table_remove(traverse.path)\r\n                  else\r\n                     array[elem_index] = null\r\n                  end\r\n               end\r\n               elem_ready = false\r\n               elem_index = elem_index + 1\r\n               startPos = startPos + 1\r\n            end\r\n         until curChar ~= ','\r\n         if elem_ready then\r\n            error('Comma is missing in JSON array at position '..startPos)\r\n         end\r\n         if traverse then\r\n            table_insert(traverse.path, elem_index)\r\n         end\r\n         object, startPos = decode(state, startPos, traverse)\r\n         if traverse then\r\n            table_remove(traverse.path)\r\n         else\r\n            array[elem_index] = object\r\n         end\r\n         elem_ready = true\r\n      end\r\n   end\r\n\r\n   --- Scans for given constants: true, false or null\r\n   -- Returns the appropriate Lua type, and the position of the next character to read.\r\n   -- @param  state        The state of JSON reader.\r\n   -- @param  startPos     The position in the string at which to start scanning.\r\n   -- @return object, int  The object (true, false or json.null) and the position at which the next character should be scanned.\r\n   function decode_scanConstant(state, startPos)\r\n      local w5 = get_word(state, startPos, 5)\r\n      local w4 = string_sub(w5, 1, 4)\r\n      if w5 == \"false\" then\r\n         return false, startPos + 5\r\n      elseif w4 == \"true\" then\r\n         return true, startPos + 4\r\n      elseif w4 == \"null\" then\r\n         return null, startPos + 4\r\n      end\r\n      error('Failed to parse JSON at position '..startPos)\r\n   end\r\n\r\n   --- Scans a number from the JSON encoded string.\r\n   -- (in fact, also is able to scan numeric +- eqns, which is not in the JSON spec.)\r\n   -- Returns the number, and the position of the next character after the number.\r\n   -- @param   state        The state of JSON reader.\r\n   -- @param   startPos     The position at which to start scanning.\r\n   -- @return  number,int   The extracted number and the position of the next character to scan.\r\n   function decode_scanNumber(state, startPos)\r\n      local stringValue, endPos = match_with_pattern(state, startPos, '^[%+%-%d%.eE]+', \"read\")\r\n      local stringEval = loadstring('return '..stringValue)\r\n      if not stringEval then\r\n         error('Failed to scan number '..stringValue..' in JSON string at position '..startPos)\r\n      end\r\n      return stringEval(), endPos\r\n   end\r\n\r\n   --- Scans a JSON object into a Lua object.\r\n   -- startPos begins at the start of the object.\r\n   -- Returns the object and the next starting position.\r\n   -- @param   state       The state of JSON reader.\r\n   -- @param   startPos    The starting position of the scan.\r\n   -- @param   traverse    (optional) table with fields \"path\" and \"callback\" for traversing JSON\r\n   -- @return  table,int   The scanned object as a table and the position of the next character to scan.\r\n   function decode_scanObject(state, startPos, traverse)\r\n      local object, elem_ready = not traverse and empty\r\n      startPos = startPos + 1\r\n      while true do\r\n         repeat\r\n            local curChar\r\n            startPos, curChar = decode_scanWhitespace(state, startPos)\r\n            if curChar == '}' then\r\n               return object, startPos + 1\r\n            elseif curChar == ',' then\r\n               startPos = startPos + 1\r\n               elem_ready = false\r\n            end\r\n         until curChar ~= ','\r\n         if elem_ready then\r\n            error('Comma is missing in JSON object at '..startPos)\r\n         end\r\n         -- Scan the key as string or unquoted identifier such as in {\"a\":1,b:2}\r\n         local key, value\r\n         key, startPos = decode(state, startPos, nil, true)\r\n         local colon\r\n         startPos, colon = decode_scanWhitespace(state, startPos)\r\n         if colon ~= ':' then\r\n            error('JSON object key-value assignment mal-formed at '..startPos)\r\n         end\r\n         startPos = decode_scanWhitespace(state, startPos + 1)\r\n         if traverse then\r\n            table_insert(traverse.path, key)\r\n         end\r\n         value, startPos = decode(state, startPos, traverse)\r\n         if traverse then\r\n            table_remove(traverse.path)\r\n         else\r\n            if object == empty then\r\n               object = {}\r\n            end\r\n            object[key] = value\r\n         end\r\n         elem_ready = true\r\n      end  -- infinite loop while key-value pairs are found\r\n   end\r\n\r\n   --- Scans JSON string for an identifier (unquoted key name inside object)\r\n   -- Returns the string extracted as a Lua string, and the position after the closing quote.\r\n   -- @param  state        The state of JSON reader.\r\n   -- @param  startPos     The starting position of the scan.\r\n   -- @return string,int   The extracted string as a Lua string, and the next character to parse.\r\n   function decode_scanIdentifier(state, startPos)\r\n      local identifier, idx = match_with_pattern(state, startPos, '^[%w_%-%$]+', \"read\")\r\n      if identifier == \"\" then\r\n         error('JSON String decoding failed: missing key name at position '..startPos)\r\n      end\r\n      return identifier, idx\r\n   end\r\n\r\n   -- START SoniEx2\r\n   -- Initialize some things used by decode_scanString\r\n   -- You know, for efficiency\r\n   local escapeSequences = { t = \"\\t\", f = \"\\f\", r = \"\\r\", n = \"\\n\", b = \"\\b\" }\r\n   -- END SoniEx2\r\n\r\n   --- Scans a JSON string from the opening quote to the end of the string.\r\n   -- Returns the string extracted as a Lua string, and the position after the closing quote.\r\n   -- @param  state        The state of JSON reader.\r\n   -- @param  startPos     The starting position of the scan.\r\n   -- @return string,int   The extracted string as a Lua string, and the next character to parse.\r\n   function decode_scanString(state, startPos)\r\n      local t, idx, surrogate_pair_started, regular_part = {}, startPos + 1\r\n      while true do\r\n         regular_part, idx = match_with_pattern(state, idx, '^[^\"\\\\]+', \"read\")\r\n         table_insert(t, regular_part)\r\n         local w6 = get_word(state, idx, 6)\r\n         local c = string_sub(w6, 1, 1)\r\n         if c == '\"' then\r\n            return table_concat(t), idx + 1\r\n         elseif c == '\\\\' then\r\n            local esc = string_sub(w6, 2, 2)\r\n            if esc == \"u\" then\r\n               local n = tonumber(string_sub(w6, 3), 16)\r\n               if not n then\r\n                  error(\"String decoding failed: bad Unicode escape \"..w6..\" at position \"..idx)\r\n               end\r\n               -- Handling of UTF-16 surrogate pairs\r\n               if n >= 0xD800 and n < 0xDC00 then\r\n                  surrogate_pair_started, n = n\r\n               elseif n >= 0xDC00 and n < 0xE000 then\r\n                  n, surrogate_pair_started = surrogate_pair_started and (surrogate_pair_started - 0xD800) * 0x400 + (n - 0xDC00) + 0x10000\r\n               end\r\n               if n then\r\n                  -- Convert unicode codepoint n (0..0x10FFFF) to UTF-8 string\r\n                  local x\r\n                  if n < 0x80 then\r\n                     x = string_char(n % 0x80)\r\n                  elseif n < 0x800 then\r\n                     -- [110x xxxx] [10xx xxxx]\r\n                     x = string_char(0xC0 + (math_floor(n/64) % 0x20), 0x80 + (n % 0x40))\r\n                  elseif n < 0x10000 then\r\n                     -- [1110 xxxx] [10xx xxxx] [10xx xxxx]\r\n                     x = string_char(0xE0 + (math_floor(n/64/64) % 0x10), 0x80 + (math_floor(n/64) % 0x40), 0x80 + (n % 0x40))\r\n                  else\r\n                     -- [1111 0xxx] [10xx xxxx] [10xx xxxx] [10xx xxxx]\r\n                     x = string_char(0xF0 + (math_floor(n/64/64/64) % 8), 0x80 + (math_floor(n/64/64) % 0x40), 0x80 + (math_floor(n/64) % 0x40), 0x80 + (n % 0x40))\r\n                  end\r\n                  table_insert(t, x)\r\n               end\r\n               idx = idx + 6\r\n            else\r\n               table_insert(t, escapeSequences[esc] or esc)\r\n               idx = idx + 2\r\n            end\r\n         else\r\n            error('String decoding failed: missing closing \" for string at position '..startPos)\r\n         end\r\n      end\r\n   end\r\n\r\n   --- Scans a JSON string skipping all whitespace from the current start position.\r\n   -- Returns the position of the first non-whitespace character.\r\n   -- @param   state      The state of JSON reader.\r\n   -- @param   startPos   The starting position where we should begin removing whitespace.\r\n   -- @return  int,char   The first position where non-whitespace was encountered, non-whitespace char.\r\n   function decode_scanWhitespace(state, startPos)\r\n      while true do\r\n         startPos = match_with_pattern(state, startPos, '^[ \\n\\r\\t]+', \"skip\")\r\n         local w2 = get_word(state, startPos, 2)\r\n         if w2 == '/*' then\r\n            local endPos = skip_until_word(state, startPos + 2, '*/')\r\n            if not endPos then\r\n               error(\"Unterminated comment in JSON string at \"..startPos)\r\n            end\r\n            startPos = endPos\r\n         else\r\n            local next_char = string_sub(w2, 1, 1)\r\n            if next_char == '' then\r\n               error('Unexpected end of JSON')\r\n            end\r\n            return startPos, next_char\r\n         end\r\n      end\r\n   end\r\n\r\n   --- Encodes a string to be JSON-compatible.\r\n   -- This just involves backslash-escaping of quotes, slashes and control codes\r\n   -- @param   s        The string to return as a JSON encoded (i.e. backquoted string)\r\n   -- @return  string   The string appropriately escaped.\r\n   local escapeList = {\r\n         ['\"']  = '\\\\\"',\r\n         ['\\\\'] = '\\\\\\\\',\r\n         ['/']  = '\\\\/',\r\n         ['\\b'] = '\\\\b',\r\n         ['\\f'] = '\\\\f',\r\n         ['\\n'] = '\\\\n',\r\n         ['\\r'] = '\\\\r',\r\n         ['\\t'] = '\\\\t',\r\n         ['\\127'] = '\\\\u007F'\r\n   }\r\n   function encodeString(s)\r\n      if type(s) == 'number' then\r\n         s = math_type(s) == 'integer' and tostring(s) or string_format('%.f', s)\r\n      end\r\n      return string_gsub(s, \".\", function(c) return escapeList[c] or c:byte() < 32 and string_format('\\\\u%04X', c:byte()) end)\r\n   end\r\n\r\n   -- Determines whether the given Lua type is an array or a table / dictionary.\r\n   -- We consider any table an array if it has indexes 1..n for its n items, and no other data in the table.\r\n   -- I think this method is currently a little 'flaky', but can't think of a good way around it yet...\r\n   -- @param   t                 The table to evaluate as an array\r\n   -- @return  boolean,number    True if the table can be represented as an array, false otherwise.\r\n   --                            If true, the second returned value is the maximum number of indexed elements in the array.\r\n   function isArray(t)\r\n      -- Next we count all the elements, ensuring that any non-indexed elements are not-encodable\r\n      -- (with the possible exception of 'n')\r\n      local maxIndex = 0\r\n      for k, v in pairs(t) do\r\n         if type(k) == 'number' and math_floor(k) == k and 0 <= k and k <= 1e6 then  -- k,v is an indexed pair\r\n            if not isEncodable(v) then  -- All array elements must be encodable\r\n               return false\r\n            end\r\n            maxIndex = math_max(maxIndex, k)\r\n         elseif not (k == 'n' and v == #t) then  -- if it is n, then n does not hold the number of elements\r\n            if isConvertibleToString(k) and isEncodable(v) then\r\n               return false\r\n            end\r\n         end -- End of k,v not an indexed pair\r\n      end  -- End of loop across all pairs\r\n      return true, maxIndex\r\n   end\r\n\r\n   --- Determines whether the given Lua object / table / value can be JSON encoded.\r\n   -- The only types that are JSON encodable are: string, boolean, number, nil, table and special tables json.null and json.empty.\r\n   -- @param   o        The object to examine.\r\n   -- @return  boolean  True if the object should be JSON encoded, false if it should be ignored.\r\n   function isEncodable(o)\r\n      local t = type(o)\r\n      return t == 'string' or t == 'boolean' or t == 'number' and isRegularNumber(o) or t == 'nil' or t == 'table'\r\n   end\r\n\r\n   --- Determines whether the given Lua object / table / variable can be a JSON key.\r\n   -- Integer Lua numbers are allowed to be considered as valid string keys in JSON.\r\n   -- @param   o        The object to examine.\r\n   -- @return  boolean  True if the object can be converted to a string, false if it should be ignored.\r\n   function isConvertibleToString(o)\r\n      local t = type(o)\r\n      return t == 'string' or t == 'number' and isRegularNumber(o) and (math_type(o) == 'integer' or math_floor(o) == o)\r\n   end\r\n\r\n   local is_Inf_or_NaN = {[tostring(1/0)]=true, [tostring(-1/0)]=true, [tostring(0/0)]=true, [tostring(-(0/0))]=true}\r\n   --- Determines whether the given Lua number is a regular number or Inf/Nan.\r\n   -- @param   v        The number to examine.\r\n   -- @return  boolean  True if the number is a regular number which may be encoded in JSON.\r\n   function isRegularNumber(v)\r\n      return not is_Inf_or_NaN[tostring(v)]\r\n   end\r\n\r\nend\r\n\r\nlocal decrypttableandreturn = Jamin.decode(\"PUT_ENCRYPTEDJAMINVALUE_HERE\")\r\n\r\nlocal json_as_string = json.encode(decrypttableandreturn)\r\n\r\nreturn json_as_string.gsub(json_as_string, \"\\\\\", \"\")\r\n\r\n-------------------- End Custom code for PSMultiServer\r\n\r\n-------------------------------------------------------------------------------\r\n\r\n--[[\r\nprint(\"[Test Jamin]\")\r\n\r\nlocal testTable = {\r\n    [\"testKey\"] = 42,\r\n    ['elbow'] = \"spam&eggs\",\r\n    [1] = {\r\n        [\"arg\"] = {\r\n            [\"test\"] = \"ogre\"\r\n        }\r\n    }\r\n}\r\n\r\nlocal encoded = Jamin.encode(testTable)\r\n\r\nprint(encoded)\r\n\r\nlocal testTable2 = Jamin.decode(encoded)\r\n\r\nfor k,v in pairs(testTable2) do\r\n    print(k,v)\r\nend\r\n\r\nprint(Jamin.decode(Jamin.encode(nil)))\r\n\r\nfunction testEscaping( str )\r\n    local escaped = escapeString(str)\r\n    local xformed = unescapeString(escaped)\r\n\r\n    if str ~= xformed then\r\n        print(string.format(\"str: %s\", str))\r\n        print(string.format(\"escaped: %s\", escaped))\r\n        print(string.format(\"xformed: %s\", xformed))\r\n        error(\"unesaping and escaped string shouldn't change the string!\")\r\n    end\r\nend\r\n\r\ntestEscaping(\"spam\")\r\ntestEscaping(\"#pam\")\r\ntestEscaping(\"#pa\\0m\")\r\ntestEscaping(\"#pam\\0\")\r\n\r\n-- Lets's test everything...\r\nlocal chars = {}\r\n\r\nfor i = 0,255 do\r\n    table.insert(chars, string.char(i))\r\nend\r\n\r\nlocal allstr = table.concat(chars)\r\n\r\n-- To ensure that every char is midway in a string...\r\ntestEscaping(allstr .. allstr)\r\n\r\nlocal freq = {}\r\n\r\nlocal escaped = escapeString(allstr)\r\n\r\nfor i = 1, #escaped do\r\n    local char = escaped:byte(i)\r\n    if freq[char] then\r\n        freq[char] = freq[char] + 1\r\n    else\r\n        freq[char] = 1\r\n    end\r\nend\r\n\r\nlocal count = 0\r\nfor i = 0, 255 do\r\n    print(i, freq[i])\r\n\r\n    count = count + (freq[i] or 0)\r\nend\r\n\r\nassert(count == #escaped)\r\n\r\nprint(string.format(\"#allstr = %d, #escaped = %d\", #allstr, #escaped))\r\n\r\n\r\nprint(\"[end Test Jamin]\")\r\n--]]\r\n\r\n\r\n-- local guids = {\r\n--     -- '49579E5D-DDAB4E3F-901EF612-F5FC7B36',\r\n--     -- '072F62A3-86CE44BC-9B85BDE6-0BD646A3',\r\n--     '33229FDB-120042BB-8B13026E-4CD7EBF0',\r\n-- }\r\n\r\n-- print(\"============ JAMIN TEST ================\")\r\n-- print(Jamin.encode( guids ))\r\n-- print(\"========================================\")\r\n";
        
        private static string jaminencrypt = "--\r\n-- Encode Lua values as strings and decode those strings back into Lua values.\r\n-- The name is a bad pun on Bencode on which it is based.\r\n--\r\n-- Jamin.encode( table ) returns string or error\r\n-- Jamin.decode( string ) returns table or error\r\n--\r\n-- nil -> 'z;'\r\n-- bool -> 'b' ('t' | 'f') ';'\r\n-- number -> 'n' <integer-or-float> ';'\r\n-- string -> 's' [0-9]+ ':' <bytes> ';'\r\n-- vector -> 'v' <integer-or-float> ' ' <integer-or-float> ' ' <integer-or-float> ' ' <integer-or-float> ';'\r\n-- table -> 't' { key value } ';'\r\n-- key -> number | string\r\n-- value -> bool | number | string | vector | table | nil\r\n--\r\n\r\nJamin = {}\r\n\r\n\r\n-- Need a table of 'safe' charcaters that can be sent via HttpPostData and XML\r\n-- without escaping or expansion.\r\n\r\nlocal _letters = {}  -- index -> char\r\n\r\nlocal _special = {\r\n    [' '] = true,  -- Multiple spaces are concatenated by the XML parser\r\n    [\"'\"] = true,\r\n    ['\"'] = true,\r\n    ['<'] = true,\r\n    ['>'] = true,\r\n    ['#'] = true,  -- Used as an escape character in Jamin.\r\n    ['%'] = true,  -- HttpPostData will hang if this is sent.\r\n    ['&'] = true,  -- Used to escape '%' in HttpPostData mesaages.\r\n}\r\n\r\nfor i = 33, 126 do\r\n    local char = string.char(i)\r\n\r\n    if not _special[char] then\r\n        _letters[#_letters+1] = char\r\n    end\r\nend\r\n\r\nlocal _alphabet = table.concat(_letters)\r\nlocal _numLetters = #_alphabet\r\n\r\n-- _letters is of type index -> char, this is the inverse mapping from chars to\r\n-- integers.\r\nlocal _invLetters = {}\r\n\r\nfor index, letter in ipairs(_letters) do\r\n    _invLetters[letter] = index\r\nend\r\n\r\nlocal _word = {}\r\n\r\nlocal _wordMT = {\r\n    __index =\r\n        function ( tbl, val )\r\n            assert(val == math.floor(val))\r\n            assert(val >= 1)\r\n\r\n            -- A word of length val, made only from _letters can have this many\r\n            -- distinct values\r\n            local size = (_numLetters)^val\r\n            -- Smallest value a word of length val can represent.\r\n            local min = -math.floor(size * 0.5)\r\n            -- Largest value a word of length val can represent.\r\n            local max = math.floor(size * 0.5) - ((size+1) % 2)\r\n            -- Add this to change from [min..max] to [0..size-1]\r\n            local offset = -min\r\n\r\n            assert(size > 0)\r\n            assert((max - min) + 1 == size)\r\n            assert(min + offset == 0)\r\n\r\n            local data = {\r\n                size = size,\r\n                min = min,\r\n                max = max,\r\n                offset = offset,\r\n                length = val,\r\n            }\r\n\r\n            -- printf('_word[%d] = size:%s min:%s max:%s offset:%s',\r\n            --        val,\r\n            --        tostring(size),\r\n            --        tostring(min),\r\n            --        tostring(max),\r\n            --        tostring(offset))\r\n\r\n            rawset(tbl, val, data)\r\n\r\n            return data\r\n        end\r\n}\r\n\r\nsetmetatable(_word, _wordMT)\r\n\r\n\r\n\r\nlocal function encodeNil( value )\r\n    assert(type(value) == \"nil\")\r\n\r\n    return 'z;'\r\nend\r\n\r\nlocal function encodeBoolean( value )\r\n    assert(type(value) == \"boolean\")\r\n\r\n    if value then\r\n        return \"bt;\"\r\n    else\r\n        return \"bf;\"\r\n    end\r\nend\r\n\r\nlocal function encodeNumber( value )\r\n    assert(type(value) == \"number\" or Type(value) == \"BigInt\")\r\n\r\n    if type(value) == 'BigInt' then\r\n        return string.format(\"n%s;\", tostring(value))\r\n\telseif value == math.floor(value) then\r\n\t\treturn string.format(\"n%d;\", value)\r\n\telse\r\n\t\treturn string.format(\"n%s;\", tostring(value))\r\n\tend\r\nend\r\n\r\n-- Escape characters outside a safe ASCII subset. This is needed because Lua\r\n-- allows '\\0' mid-string and Home can give us utf8 strings.\r\n--\r\n-- The characters in the range [32..127] are considered safe. Space,\r\n-- alphanumeric and punctuation characters are in this range.\r\n-- An escape is in one of two forms:\r\n--   '#xxx' where xxx is the 0 extended, decimal code for the character.\r\n--   '##' represents '#' in an unescaped string.\r\n--\r\nlocal function escapeString( str )\r\n    local partial = str:gsub(\"#\", \"##\")\r\n\r\n    local function expandUnsafeCharacters( str )\r\n        local chars = {}\r\n        for idx = 1, #str do\r\n            table.insert(chars, string.format(\"#%03d\", str:byte(idx)))\r\n        end\r\n        return table.concat(chars)\r\n    end\r\n\r\n    return partial:gsub(\"([^%w%p ]+)\",  expandUnsafeCharacters)\r\nend\r\n\r\nlocal function encodeString( value )\r\n    local escaped = escapeString(value)\r\n\r\n    return string.format(\"s%d:%s;\", escaped:len(), escaped)\r\nend\r\n\r\nlocal function encodeVector( value )\r\n    return string.format(\"v%f %f %f %f;\", value:X(), value:Y(), value:Z(), value:W())\r\nend\r\n\r\nfunction iskey( value )\r\n    local t = type(value)\r\n\r\n    return t == \"number\" or t == \"string\"\r\nend\r\n\r\n-- The function below can create a lot of garbage so care has to be taken\r\n-- to merge intermediate results and call the collector.\r\n-- NB: Need to improve this function:\r\n--     - The length of the strings needs to be taken into account.\r\n--     - The garbage collector can be called quite often.\r\nlocal function encodeTable( value, isCoroutine )\r\n    local strings = { 't' }\r\n    local maxlen = 100\r\n\r\n    for k, v in pairs(value) do\r\n        assert(iskey(k), string.format(\"%s can't be used as a key only strings and numbers allowed\", tostring(k)))\r\n\r\n        local kstr\r\n        if isCoroutine then\r\n            coroutine.yield(false)\r\n            kstr = Jamin.coroutineEncode(k)\r\n        else\r\n            kstr = Jamin.encode(k)\r\n        end\r\n\r\n        local vstr\r\n        if isCoroutine then\r\n            coroutine.yield(false)\r\n            vstr = Jamin.coroutineEncode(v)\r\n        else\r\n            vstr = Jamin.encode(v)\r\n        end\r\n\r\n        strings[#strings+1] = kstr\r\n        strings[#strings+1] = vstr\r\n\r\n        if #strings > maxlen then\r\n            strings = { table.concat(strings) }\r\n\r\n            collectgarbage('collect')\r\n        end\r\n    end\r\n\r\n    strings[#strings+1] = ';'\r\n\r\n    local result = table.concat(strings)\r\n\r\n    -- collectgarbage('collect')\r\n\r\n    return result\r\nend\r\n\r\nlocal function encodeFunction( value )\r\n    return value()\r\nend\r\n\r\nlocal _minIntegerArrayElement = -(2^24)\r\nlocal _maxIntegerArrayElement = 2^24\r\n\r\nfunction _isValidIntegerArrayElement( value )\r\n    if value == math.floor(value) then\r\n        if _minIntegerArrayElement <= value and value <= _maxIntegerArrayElement then\r\n            return true\r\n        end\r\n    end\r\n\r\n    return false\r\nend\r\n\r\nfunction calcIntegerArrayWordLength( array )\r\n    local result = 0\r\n\r\n    if #array > 0 then\r\n        local min = _maxIntegerArrayElement\r\n        local max = _minIntegerArrayElement\r\n\r\n        for _, v in ipairs(array) do\r\n            assert(_isValidIntegerArrayElement(v))\r\n\r\n            min = math.min(min, v)\r\n            max = math.max(max, v)\r\n        end\r\n\r\n        local normedRange = math.max(math.abs(min), math.abs(max)) * 2\r\n\r\n        result = math.ceil(math.log(normedRange) / math.log(_numLetters))\r\n\r\n        -- printf('[%s..%s] %s -> %d', tostring(min), tostring(max), tostring(normedRange), result)\r\n    end\r\n\r\n    return result\r\nend\r\n\r\nfunction Jamin.encodeIntegerArrayViaIterator( iterator, wordlen )\r\n    local word = _word[wordlen]\r\n    local results = { '' }  -- don't know how long it is yet.\r\n    local count = 0\r\n\r\n    for element in iterator do\r\n        count = count + 1\r\n        -- [word.min..word.max] -> [0..word.size-1]\r\n        local normed = element + word.offset\r\n\r\n        assert(0 <= normed and normed < word.size)\r\n\r\n        -- Little Endian\r\n        for i = 0, wordlen - 1 do\r\n            local index = math.floor(normed / (_numLetters)^i) % _numLetters\r\n            -- printf('val:%d normed:%d i:%d index:%d', val, normed, i, index)\r\n            results[#results+1] = _letters[index+1]\r\n        end\r\n    end\r\n\r\n    results[1] = string.format('i%d,%d:', wordlen, count)\r\n\r\n    assert(#results == (count * wordlen) + 1)\r\n\r\n    results[#results+1] = ';'\r\n\r\n    return table.concat(results)\r\nend\r\n\r\nfunction Jamin.encodeIntegerArray( value, wordlen )\r\n    assert(type(value) == 'table')\r\n\r\n    wordlen = wordlen or calcIntegerArrayWordLength(value)\r\n\r\n    local word = _word[wordlen]\r\n    local results = { string.format('i%d,%d:', wordlen, #value) }\r\n\r\n    for _, element in ipairs(value) do\r\n        -- [word.min..word.max] -> [0..word.size-1]\r\n        local normed = element + word.offset\r\n\r\n        assert(0 <= normed and normed < word.size)\r\n\r\n        -- Little Endian\r\n        for i = 0, wordlen - 1 do\r\n            local index = math.floor(normed / (_numLetters)^i) % _numLetters\r\n            -- printf('val:%d normed:%d i:%d index:%d', val, normed, i, index)\r\n            results[#results+1] = _letters[index+1]\r\n        end\r\n    end\r\n\r\n    assert(#results == (#value * wordlen) + 1)\r\n\r\n    results[#results+1] = ';'\r\n\r\n    return table.concat(results)\r\nend\r\n\r\n\r\nlocal encoders = {\r\n    [\"nil\"] = encodeNil,\r\n    [\"boolean\"] = encodeBoolean,\r\n    [\"number\"] = encodeNumber,\r\n    [\"BigInt\"] = encodeNumber,\r\n    [\"string\"] = encodeString,\r\n    [\"Vector4\"] = encodeVector,\r\n    [\"table\"] = encodeTable,\r\n    [\"function\"] = encodeFunction,\r\n}\r\n\r\n-- Type() is from the Home HDK for use on HDK values.\r\nlocal function _type( x )\r\n    local result = type(x)\r\n\r\n    if result == \"userdata\" then\r\n        result = Type(x)\r\n    end\r\n\r\n    return result\r\nend\r\n\r\nlocal function encodeError( value )\r\n    error(string.format(\"can't encode %s of type %s\", tostring(value), _type(value)))\r\nend\r\n\r\nfunction Jamin.encode( value )\r\n    local encoder = encoders[_type(value)] or encodeError\r\n\r\n    return encoder(value)\r\nend\r\n\r\nfunction Jamin.coroutineEncode( value )\r\n    local encoder = encoders[_type(value)] or encodeError\r\n\r\n    return encoder(value, true)\r\nend\r\n\r\n-------------------------------------------------------------------------------\r\n\r\n\r\n-- str:sub(pos, pos) == \"z\"\r\nlocal function decodeNil( str, pos )\r\n    local start, finish = str:find(\"z;\", pos)\r\n\r\n    if start == pos and finish == pos + 1 then\r\n        return nil, pos + 2\r\n    else\r\n        error(string.format(\"%s is not a valid nil at pos %d\", str:sub(pos, pos+2), pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"b\"\r\nlocal function decodeBoolean( str, pos )\r\n    local encoded, finish = str:match(\"b([tf]);()\", pos)\r\n\r\n    if encoded then\r\n        return encoded == \"t\", pos + 3\r\n    else\r\n        error(string.format(\"%s is not a valid boolean at pos %d\", str:sub(pos, pos+2), pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"n\"\r\nlocal function decodeNumber( str, pos )\r\n    local encoded, finish = str:match(\"n([^;]+);()\", pos)\r\n\r\n    local decoded = tonumber(encoded)\r\n    if decoded > 2^24 or decoded < -2^24 then\r\n        decoded = BigInt.Create('64', encoded)\r\n    end\r\n\r\n    if decoded then\r\n        return decoded, finish\r\n    else\r\n        error(string.format(\"'%s' is not a valid number at pos %d\", encoded, pos))\r\n    end\r\nend\r\n\r\nlocal function _unescapeUnsafeChracters( str )\r\n    return string.char(tonumber(str))\r\nend\r\n\r\n\r\n-- See escapeString() above for more details\r\nlocal function unescapeString( str )\r\n    local partial = str:gsub(\"##\", \"#\")\r\n\r\n    return partial:gsub(\"#(%d%d%d)\", _unescapeUnsafeChracters)\r\nend\r\n\r\n-- str:sub(pos, pos) == \"s\"\r\nlocal function decodeString( str, pos )\r\n    local encoded = str:match(\"s([%d]+):\", pos)\r\n    local length = tonumber(encoded)\r\n\r\n    if length then\r\n        local start = pos + 2 + encoded:len()\r\n        local finish = start + (length-1)\r\n\r\n        if str:sub(finish+1, finish+1) == \";\" then\r\n            local escaped = str:sub(start, finish)\r\n            local unescaped = unescapeString(escaped)\r\n            return unescaped, finish + 2\r\n        else\r\n            error(string.format(\"couldn't find terminating ';' for string at pos %d - context: %q\", pos, str:sub(pos, finish+1)))\r\n        end\r\n    else\r\n        error(string.format(\"couldn't find length of string at pos %d\", pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"v\"\r\nlocal function decodeVector( str, pos )\r\n    local encX, encY, encZ, encW, finish = str:match(\"v([^ ;]+) ([^ ;]+) ([^ ;]+) ([^ ;]+);()\", pos)\r\n    local x, y, z, w = tonumber(encX), tonumber(encY), tonumber(encZ), tonumber(encW)\r\n\r\n    if x and y and z and w then\r\n        return Vector4.Create(x, y, z, w), finish\r\n    else\r\n        error(string.format(\"couldn't parse vector at pos %d\", pos))\r\n    end\r\nend\r\n\r\n-- str:sub(pos, pos) == \"t\"\r\nlocal function decodeTable( str, pos )\r\n    local result = {}\r\n    local key, cursor = nil, pos + 1\r\n\r\n    while str:sub(cursor, cursor) ~= \";\" do\r\n        key, cursor = Jamin.decode(str, cursor)\r\n        assert(iskey(key), string.format(\"%s can't be used as a key only strings and numbers allowed\", tostring(key)))\r\n\r\n        value, cursor = Jamin.decode(str, cursor)\r\n\r\n        result[key] = value\r\n    end\r\n\r\n    return result, cursor+1\r\nend\r\n\r\nfunction Jamin.decodeIntegerArray( str, pos )\r\n    pos = pos or 1\r\n\r\n    local encWordlen, encNumElements = str:match(\"i([%d]+),([%d]+):\", pos)\r\n    local wordlen = tonumber(encWordlen)\r\n    local numElements = tonumber(encNumElements)\r\n\r\n    if wordlen and numElements then\r\n        local word = _word[wordlen]\r\n        local start = pos + 3 + encWordlen:len() + encNumElements:len()\r\n        local finish = start + (wordlen * numElements) - 1\r\n\r\n        if str:sub(finish+1, finish+1) == \";\" then\r\n            local encoded = str:sub(start, finish)\r\n\r\n            -- printf('encoded: %s', encoded)\r\n\r\n            local result= {}\r\n            local count = 0\r\n            local normed = 0\r\n\r\n            -- Should be more memory efficient matching each char seperately as\r\n            -- they're already in the _letters and _invLetters tables.\r\n            for encDigit in encoded:gmatch('.') do\r\n                local wordIndex = count % wordlen\r\n\r\n                local digit = (_invLetters[encDigit] - 1) * ((_numLetters)^wordIndex)\r\n\r\n                -- print('encdigit:', encdigit, 'wordIndex:', wordIndex, 'digit:', digit)\r\n                normed = normed + digit\r\n\r\n                -- Is this the last letter of a word\r\n                if wordIndex == wordlen - 1 then\r\n                    result[#result+1] = normed - word.offset\r\n                    normed = 0\r\n                end\r\n\r\n                count = count + 1\r\n            end\r\n\r\n            return result, finish + 2\r\n        else\r\n            error(string.format(\"couldn't find terminating ';' for integer-array at pos %d, found %q\", pos, str:sub(finish+1, finish+1)))\r\n        end\r\n    else\r\n        error(string.format(\"couldn't find one or both of wordlen and #elements in integer-array at pos %d\", pos))\r\n    end\r\nend\r\n\r\nlocal decoders = {\r\n    [\"z\"] = decodeNil,\r\n    [\"b\"] = decodeBoolean,\r\n    [\"n\"] = decodeNumber,\r\n    [\"s\"] = decodeString,\r\n    [\"v\"] = decodeVector,\r\n    [\"t\"] = decodeTable,\r\n    [\"i\"] = decodeIntegerArray,\r\n}\r\n\r\nlocal function decodeError( str, pos )\r\n    error(string.format(\"no decoder for %q at pos %d\", str:sub(pos, pos), pos))\r\nend\r\n\r\nfunction Jamin.decode( str, pos )\r\n    pos = pos or 1\r\n\r\n    local decoder = decoders[str:sub(pos, pos)] or decodeError\r\n\r\n    return decoder(str, pos)\r\nend\r\n\r\n-------------------- Custom code for PSMultiServer\r\n\r\nlocal TableFromInput = PUT_TABLEINPUT_HERE\r\n\r\nreturn Jamin.encode(TableFromInput)\r\n\r\n-------------------- End Custom code for PSMultiServer\r\n\r\n-------------------------------------------------------------------------------\r\n\r\n--[[\r\nprint(\"[Test Jamin]\")\r\n\r\nlocal testTable = {\r\n    [\"testKey\"] = 42,\r\n    ['elbow'] = \"spam&eggs\",\r\n    [1] = {\r\n        [\"arg\"] = {\r\n            [\"test\"] = \"ogre\"\r\n        }\r\n    }\r\n}\r\n\r\nlocal encoded = Jamin.encode(testTable)\r\n\r\nprint(encoded)\r\n\r\nlocal testTable2 = Jamin.decode(encoded)\r\n\r\nfor k,v in pairs(testTable2) do\r\n    print(k,v)\r\nend\r\n\r\nprint(Jamin.decode(Jamin.encode(nil)))\r\n\r\nfunction testEscaping( str )\r\n    local escaped = escapeString(str)\r\n    local xformed = unescapeString(escaped)\r\n\r\n    if str ~= xformed then\r\n        print(string.format(\"str: %s\", str))\r\n        print(string.format(\"escaped: %s\", escaped))\r\n        print(string.format(\"xformed: %s\", xformed))\r\n        error(\"unesaping and escaped string shouldn't change the string!\")\r\n    end\r\nend\r\n\r\ntestEscaping(\"spam\")\r\ntestEscaping(\"#pam\")\r\ntestEscaping(\"#pa\\0m\")\r\ntestEscaping(\"#pam\\0\")\r\n\r\n-- Lets's test everything...\r\nlocal chars = {}\r\n\r\nfor i = 0,255 do\r\n    table.insert(chars, string.char(i))\r\nend\r\n\r\nlocal allstr = table.concat(chars)\r\n\r\n-- To ensure that every char is midway in a string...\r\ntestEscaping(allstr .. allstr)\r\n\r\nlocal freq = {}\r\n\r\nlocal escaped = escapeString(allstr)\r\n\r\nfor i = 1, #escaped do\r\n    local char = escaped:byte(i)\r\n    if freq[char] then\r\n        freq[char] = freq[char] + 1\r\n    else\r\n        freq[char] = 1\r\n    end\r\nend\r\n\r\nlocal count = 0\r\nfor i = 0, 255 do\r\n    print(i, freq[i])\r\n\r\n    count = count + (freq[i] or 0)\r\nend\r\n\r\nassert(count == #escaped)\r\n\r\nprint(string.format(\"#allstr = %d, #escaped = %d\", #allstr, #escaped))\r\n\r\n\r\nprint(\"[end Test Jamin]\")\r\n--]]\r\n\r\n\r\n-- local guids = {\r\n--     -- '49579E5D-DDAB4E3F-901EF612-F5FC7B36',\r\n--     -- '072F62A3-86CE44BC-9B85BDE6-0BD646A3',\r\n--     '33229FDB-120042BB-8B13026E-4CD7EBF0',\r\n-- }\r\n\r\n-- print(\"============ JAMIN TEST ================\")\r\n-- print(Jamin.encode( guids ))\r\n-- print(\"========================================\")\r\n";
        public static async Task ProcessRequest(HttpListenerContext context, string userAgent)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                // Extract the HTTP method and the relative path
                string httpMethod = request.HttpMethod;
                string url = request.Url.LocalPath;

                Console.WriteLine($"OHS : Received {httpMethod} request for {url}");

                // Split the URL into segments
                string[] segments = url.Trim('/').Split('/');

                // Combine the folder segments into a directory path
                string directoryPath = Path.Combine(Directory.GetCurrentDirectory() + "/wwwroot/", string.Join("/", segments.Take(segments.Length - 1).ToArray()));

                // Process the request based on the HTTP method
                string filePath = Path.Combine(Directory.GetCurrentDirectory() + "/wwwroot/", url.Substring(1));

                switch (httpMethod)
                {
                    case "POST":

                        try
                        {
                            if (request.Url.AbsolutePath.Contains("global/set/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => set(context, userAgent, directoryPath, true, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to global/set/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to global/set/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("global/get/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => get(context, userAgent, directoryPath, true, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to global/get/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to global/get/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("user/getwritekey/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => user_getwritekey(context, userAgent, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to user/getwritekey/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to user/getwritekey/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("user/set/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => set(context, userAgent, directoryPath, false, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to user/set/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to user/set/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("user/get/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => get(context, userAgent, directoryPath, false, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to user/get/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to user/get/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("leaderboard/requestbyusers/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => leaderboard_requestbyusers(context, userAgent, directoryPath, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to leaderboard/requestbyusers/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to leaderboard/requestbyusers/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("leaderboard/requestbyrank/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => leaderboard_requestbyrank(context, userAgent, directoryPath, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to leaderboard/requestbyrank/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to leaderboard/requestbyrank/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("leaderboard/updatessameentry/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => leaderboard_updatessameentry(context, userAgent, directoryPath, ""));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to leaderboard/updatessameentry/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to leaderboard/updatessameentry/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("/statistic/set/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                                        Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                                        // Execute the Lua script and get the result
                                        object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", data.GetParameterValue("data").Substring(8)));

                                        if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                                        {
                                            Console.WriteLine($"OHS Server : {userAgent} issued a /statistic/set/ command : Json result - {returnValues[0]?.ToString()}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"OHS Server : {userAgent} Requested a /statistic/set/ method, but the lua errored out!");
                                        }

                                        // Execute the Lua script and get the result
                                        object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", "{ [\"status\"] = \"success\" }"));

                                        string dataforohs = "";

                                        if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                                        {
                                            dataforohs = returnValues2nd[0]?.ToString();
                                        }
                                        else
                                        {
                                            Console.WriteLine($"OHS Server : {userAgent} Requested a /statistic/set/ method, but the lua errored out!");
                                        }

                                        byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = 200;
                                                context.Response.ContentLength64 = postresponsetooutput.Length;
                                                context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to /statistic/set/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to /statistic/set/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else if (request.Url.AbsolutePath.Contains("/batch/"))
                            {
                                if (context.Request.ContentType != null)
                                {
                                    if (context.Request.ContentType.StartsWith("multipart/form-data"))
                                    {
                                        Task.Run(() => batch_process(context, userAgent, directoryPath));
                                    }
                                    else
                                    {
                                        Console.WriteLine($"OHS Server : {userAgent} tried to POST data to /batch/, but it's not correct so we forbid.");

                                        // Return a not allowed response
                                        byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                        if (context.Response.OutputStream.CanWrite)
                                        {
                                            try
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                                context.Response.ContentLength64 = notAllowed.Length;
                                                context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                                context.Response.OutputStream.Close();
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Client Disconnected early");
                                        }

                                        context.Response.Close();

                                        GC.Collect();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to /batch/, but it's not correct so we forbid.");

                                    // Return a not allowed response
                                    byte[] notAllowed = Encoding.UTF8.GetBytes("Not allowed.");

                                    if (context.Response.OutputStream.CanWrite)
                                    {
                                        try
                                        {
                                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                            context.Response.ContentLength64 = notAllowed.Length;
                                            context.Response.OutputStream.Write(notAllowed, 0, notAllowed.Length);
                                            context.Response.OutputStream.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Client Disconnected early and thrown an exception {ex}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Client Disconnected early");
                                    }

                                    context.Response.Close();

                                    GC.Collect();
                                }
                            }
                            else
                            {
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    request.InputStream.CopyTo(ms);

                                    // Reset the memory stream position to the beginning
                                    ms.Position = 0;

                                    // Find the number of bytes in the stream
                                    int contentLength = (int)ms.Length;

                                    // Create a byte array
                                    byte[] buffer = new byte[contentLength];

                                    // Read the contents of the memory stream into the byte array
                                    ms.Read(buffer, 0, contentLength);

                                    Console.WriteLine($"OHS Server : {userAgent} tried to POST data to our OHS but I don't know the method!! Report to GITHUB : {Encoding.UTF8.GetString(buffer)}");

                                    ms.Dispose();
                                }

                                // Return a not found response
                                byte[] notFoundResponse = Encoding.UTF8.GetBytes("Method not found");

                                if (response.OutputStream.CanWrite)
                                {
                                    try
                                    {
                                        response.StatusCode = 404;
                                        response.ContentLength64 = notFoundResponse.Length;
                                        response.OutputStream.Write(notFoundResponse, 0, notFoundResponse.Length);
                                        response.OutputStream.Close();

                                        Console.WriteLine($"OHS Method {filePath} - {httpMethod} not found");
                                    }
                                    catch (Exception ex1)
                                    {
                                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Client Disconnected early");
                                }

                                context.Response.Close();

                                GC.Collect();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"OHS Server has throw an exception in ProcessRequest while processing POST request : {ex}");

                            // Return an internal server error response
                            byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                            if (response.OutputStream.CanWrite)
                            {
                                try
                                {
                                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    response.ContentLength64 = InternnalError.Length;
                                    response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                                    response.OutputStream.Close();
                                }
                                catch (Exception ex1)
                                {
                                    Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Client Disconnected early");
                            }

                            context.Response.Close();

                            GC.Collect();
                        }

                        break;

                    default:

                        try
                        {
                            Console.WriteLine($"OHS WARNING - Host requested a method I don't know about!! Report it to GITHUB with the request : {httpMethod} request for {url} is not supported");

                            // Return a method not allowed response for unsupported methods
                            byte[] methodNotAllowedResponse = Encoding.UTF8.GetBytes("Method not allowed");

                            if (response.OutputStream.CanWrite)
                            {
                                try
                                {
                                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                                    response.ContentLength64 = methodNotAllowedResponse.Length;
                                    response.OutputStream.Write(methodNotAllowedResponse, 0, methodNotAllowedResponse.Length);
                                    response.OutputStream.Close();
                                }
                                catch (Exception ex1)
                                {
                                    Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Client Disconnected early");
                            }

                            context.Response.Close();

                            GC.Collect();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"OHS Server has throw an exception in ProcessRequest while processing the default request : {ex}");

                            // Return an internal server error response
                            byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                            if (response.OutputStream.CanWrite)
                            {
                                try
                                {
                                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    response.ContentLength64 = InternnalError.Length;
                                    response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                                    response.OutputStream.Close();
                                }
                                catch (Exception ex1)
                                {
                                    Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Client Disconnected early");
                            }

                            context.Response.Close();

                            GC.Collect();
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : an error occured in ProcessRequest - {ex}");

                context.Response.Close();

                GC.Collect();
            }

            return;
        }

        private static async Task<string> batch_process(HttpListenerContext context, string userAgent, string directorypath)
        {
            try
            {
                var multipartdata = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {multipartdata.GetParameterValue("version")}");

                string dataforohs = multipartdata.GetParameterValue("data");

                // Execute the Lua script and get the result
                object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                {
                    dataforohs = returnValues[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a /batch/ method, but the lua errored out!");
                }

                // Deserialize the JSON data into a list of commands.
                var commands = JsonConvert.DeserializeObject<BatchCommand[]>(dataforohs);

                int i = 0;

                StringBuilder resultBuilder = new StringBuilder();


                foreach (var command in commands)
                {
                    i = i + 1;

                    string resultfromcommand = "";

                    string method = command.Method;
                    string project = command.Project;
                    string data = command.Data.ToString(Formatting.None);

                    if (project == "<dummy>")
                    {
                        project = "dummy";
                    }

                    Console.WriteLine($"OHS Server : {userAgent} Requested a /batch/ method, here are the details : method | {method} - project | {project} - data | {data}");

                    switch (method)
                    {
                        case "global/get/":

                            resultfromcommand = await Task.Run(() => get(context, userAgent, directorypath + $"/{project}/", true, data));

                            break;

                        case "global/set/":

                            resultfromcommand = await Task.Run(() => set(context, userAgent, directorypath + $"/{project}/", true, data));

                            break;

                        case "userid/":

                            resultfromcommand = await Task.Run(() => user_id(context, userAgent, data));

                            break;

                        case "user/get/":

                            resultfromcommand = await Task.Run(() => get(context, userAgent, directorypath + $"/{project}/", false, data));

                            break;

                        case "user/set/":

                            resultfromcommand = await Task.Run(() => set(context, userAgent, directorypath + $"/{project}/", false, data));

                            break;

                        case "user/getwritekey/":

                            resultfromcommand = await Task.Run(() => user_getwritekey(context, userAgent, data));

                            break;

                        case "leaderboard/requestbyusers/":

                            resultfromcommand = await Task.Run(() => leaderboard_requestbyusers(context, userAgent, directorypath + $"/{project}/", data));

                            break;

                        case "leaderboard/requestbyrank/":

                            resultfromcommand = await Task.Run(() => leaderboard_requestbyrank(context, userAgent, directorypath + $"/{project}/", data));

                            break;

                        case "leaderboard/updatessameentry/":

                            resultfromcommand = await Task.Run(() => leaderboard_updatessameentry(context, userAgent, directorypath + $"/{project}/", data));

                            break;

                        default:

                            Console.WriteLine($"OHS Server : Batch requested a method I don't know about, please report it to GITHUB {method} in {project} with data {data}");

                            break;
                    }

                    if (resultfromcommand == "")
                    {
                        resultfromcommand = "{ [\"status\"] = \"failed\" }";
                    }

                    if (resultBuilder.Length == 0)
                    {
                        resultBuilder.Append($"{{ [\"status\"] = \"success\", [\"value\"] = {{ [{i}] = {resultfromcommand}");
                    }
                    else
                    {
                        resultBuilder.Append($", [{i}] = {resultfromcommand}");
                    }
                }

                resultBuilder.Append(" } }");

                dataforohs = resultBuilder.ToString();

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", dataforohs));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    dataforohs = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a /batch/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the /batch/ request : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        private static async Task<string> set(HttpListenerContext context, string userAgent, string directorypath, bool global, string batchparams)
        {
            try
            {
                int value = 0;

                string dataforohs = "";

                if (batchparams == "")
                {
                    var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                    Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                    dataforohs = data.GetParameterValue("data");

                    // Execute the Lua script and get the result
                    object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                    if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                    {
                        dataforohs = returnValues[0]?.ToString();
                    }
                    else
                    {
                        Console.WriteLine($"OHS Server : {userAgent} Requested a global/set/ or user/set/ method, but the lua errored out!");
                    }
                }
                else
                {
                    dataforohs = batchparams;
                }

                if (!global)
                {
                    // Deserialize the JSON data into a JObject
                    JObject jObject = JsonConvert.DeserializeObject<JObject>(dataforohs);

                    // Get the values from the JObject
                    value = jObject.Value<int>("value");

                    string user = jObject.Value<string>("user");

                    JToken keyToken = jObject.GetValue("key");

                    string keyName = keyToken.Value<string>();

                    string profiledatastring = directorypath + $"/User_Profiles/{user}.json";

                    if (!Directory.Exists(directorypath + "/User_Profiles/"))
                    {
                        Directory.CreateDirectory(directorypath + "/User_Profiles/");
                    }

                    if (File.Exists(profiledatastring))
                    {
                        string tempreader = "";

                        byte[] firstNineBytes = new byte[9];

                        using (FileStream fileStream = new FileStream(profiledatastring, FileMode.Open, FileAccess.Read))
                        {
                            fileStream.Read(firstNineBytes, 0, 9);
                            fileStream.Close();
                        }

                        if (HTTPserver.httpkey != "" && await Task.Run(() => Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 })))
                        {
                            byte[] src = File.ReadAllBytes(profiledatastring);
                            byte[] dst = new byte[src.Length - 9];

                            Array.Copy(src, 9, dst, 0, dst.Length);

                            tempreader = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                                        CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
                        }
                        else
                        {
                            tempreader = File.ReadAllText(profiledatastring);
                        }

                        JObject jsonObject = JObject.Parse(tempreader);

                        // Check if the key name already exists in the JSON
                        JToken existingKey = jsonObject.SelectToken($"$..{keyName}");

                        if (existingKey != null)
                        {
                            // Update the value of the existing key
                            existingKey.Replace(JToken.FromObject(value));
                        }
                        else
                        {
                            // Step 2: Add a new entry to the "Key" object
                            jsonObject["Key"][keyName] = value;
                        }

                        using (FileStream fs = new FileStream(profiledatastring, FileMode.Create))
                        {
                            // Serialize the updated JSON back to a byte array
                            byte[] updatedJsonString = Encoding.UTF8.GetBytes(jsonObject.ToString(Formatting.None));

                            if (HTTPserver.httpkey != "")
                            {
                                byte[] outfile = new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6C, 0x65, 0x64, 0x65, 0x73 };

                                byte[] encryptedbuffer = Misc.Combinebytearay(outfile, CRYPTOSPORIDIUM.TRIPLEDES.EncryptData(CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey), updatedJsonString));

                                fs.Write(encryptedbuffer, 0, encryptedbuffer.Length);
                                fs.Flush();
                                fs.Dispose();
                            }
                            else
                            {
                                fs.Write(updatedJsonString, 0, updatedJsonString.Length);
                                fs.Flush();
                                fs.Dispose();
                            }

                            Console.WriteLine($"File {profiledatastring} has been uploaded to HTTP");
                        }
                    }
                    else
                    {
                        // Create a new profile with the key field
                        OHSUserProfile newProfile = new OHSUserProfile
                        {
                            User = user,
                            Key = new JObject { { keyName, value } }
                        };

                        using (FileStream fs = new FileStream(profiledatastring, FileMode.Create))
                        {
                            // Serialize the updated JSON back to a byte array
                            byte[] updatedJsonString = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(newProfile));

                            if (HTTPserver.httpkey != "")
                            {
                                byte[] outfile = new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6C, 0x65, 0x64, 0x65, 0x73 };

                                byte[] encryptedbuffer = Misc.Combinebytearay(outfile, CRYPTOSPORIDIUM.TRIPLEDES.EncryptData(CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey), updatedJsonString));

                                fs.Write(encryptedbuffer, 0, encryptedbuffer.Length);
                                fs.Flush();
                                fs.Dispose();
                            }
                            else
                            {
                                fs.Write(updatedJsonString, 0, updatedJsonString.Length);
                                fs.Flush();
                                fs.Dispose();
                            }

                            Console.WriteLine($"File {profiledatastring} has been uploaded to HTTP");
                        }
                    }
                }
                else
                {
                    // Deserialize the JSON data into a JObject
                    JObject jObject = JsonConvert.DeserializeObject<JObject>(dataforohs);

                    // Get the values from the JObject
                    value = jObject.Value<int>("value");

                    JToken keyToken = jObject.GetValue("key");

                    string keyName = keyToken.Value<string>();

                    string globaldatastring = directorypath + "/Global.json";

                    if (!Directory.Exists(directorypath))
                    {
                        Directory.CreateDirectory(directorypath);
                    }

                    if (File.Exists(globaldatastring))
                    {
                        string tempreader = "";

                        byte[] firstNineBytes = new byte[9];

                        using (FileStream fileStream = new FileStream(globaldatastring, FileMode.Open, FileAccess.Read))
                        {
                            fileStream.Read(firstNineBytes, 0, 9);
                            fileStream.Close();
                        }

                        if (HTTPserver.httpkey != "" && await Task.Run(() => Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 })))
                        {
                            byte[] src = File.ReadAllBytes(globaldatastring);
                            byte[] dst = new byte[src.Length - 9];

                            Array.Copy(src, 9, dst, 0, dst.Length);

                            tempreader = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                                        CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
                        }
                        else
                        {
                            tempreader = File.ReadAllText(globaldatastring);
                        }

                        JObject jsonObject = JObject.Parse(tempreader);

                        // Check if the key name already exists in the JSON
                        JToken existingKey = jsonObject.SelectToken($"$..{keyName}");

                        if (existingKey != null)
                        {
                            // Update the value of the existing key
                            existingKey.Replace(JToken.FromObject(value));
                        }
                        else
                        {
                            // Step 2: Add a new entry to the "Key" object
                            jsonObject["Key"][keyName] = value;
                        }

                        using (FileStream fs = new FileStream(globaldatastring, FileMode.Create))
                        {
                            // Serialize the updated JSON back to a byte array
                            byte[] updatedJsonString = Encoding.UTF8.GetBytes(jsonObject.ToString(Formatting.None));

                            if (HTTPserver.httpkey != "")
                            {
                                byte[] outfile = new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6C, 0x65, 0x64, 0x65, 0x73 };

                                byte[] encryptedbuffer = Misc.Combinebytearay(outfile, CRYPTOSPORIDIUM.TRIPLEDES.EncryptData(CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey), updatedJsonString));

                                fs.Write(encryptedbuffer, 0, encryptedbuffer.Length);
                                fs.Flush();
                                fs.Dispose();
                            }
                            else
                            {
                                fs.Write(updatedJsonString, 0, updatedJsonString.Length);
                                fs.Flush();
                                fs.Dispose();
                            }

                            Console.WriteLine($"File {globaldatastring} has been uploaded to HTTP");
                        }
                    }
                    else
                    {
                        // Create a new profile with the key field
                        OHSGlobalProfile newProfile = new OHSGlobalProfile
                        {
                            Key = new JObject { { keyName, value } }
                        };

                        using (FileStream fs = new FileStream(globaldatastring, FileMode.Create))
                        {
                            // Serialize the updated JSON back to a byte array
                            byte[] updatedJsonString = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(newProfile));

                            if (HTTPserver.httpkey != "")
                            {
                                byte[] outfile = new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6C, 0x65, 0x64, 0x65, 0x73 };

                                byte[] encryptedbuffer = Misc.Combinebytearay(outfile, CRYPTOSPORIDIUM.TRIPLEDES.EncryptData(CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey), updatedJsonString));

                                fs.Write(encryptedbuffer, 0, encryptedbuffer.Length);
                                fs.Flush();
                                fs.Dispose();
                            }
                            else
                            {
                                fs.Write(updatedJsonString, 0, updatedJsonString.Length);
                                fs.Flush();
                                fs.Dispose();
                            }

                            Console.WriteLine($"File {globaldatastring} has been uploaded to HTTP");
                        }
                    }
                }

                if (batchparams != "")
                {
                    return value.ToString();
                }

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", $"{{ [\"status\"] = \"success\", [\"value\"] = {value.ToString()} }}"));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    dataforohs = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a global/set/ or user/set/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the global/set/ or user/set/ request : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        private static async Task<string> get(HttpListenerContext context, string userAgent, string directorypath, bool global, string batchparams)
        {
            try
            {
                string dataforohs = "";

                int value = 0;

                if (batchparams == "")
                {
                    var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                    Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                    dataforohs = data.GetParameterValue("data");

                    // Execute the Lua script and get the result
                    object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                    if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                    {
                        dataforohs = returnValues[0]?.ToString();
                    }
                    else
                    {
                        Console.WriteLine($"OHS Server : {userAgent} Requested a global/get/ or user/get/ method, but the lua errored out!");
                    }
                }
                else
                {
                    dataforohs = batchparams;
                }

                // Parsing the JSON string
                JObject inputjsonObject = JObject.Parse(dataforohs);

                if (!global)
                {
                    // Getting the value of the "user" field
                    dataforohs = (string)inputjsonObject["user"];

                    if (File.Exists(directorypath + $"/User_Profiles/{dataforohs}.json"))
                    {
                        string tempreader = "";

                        byte[] firstNineBytes = new byte[9];

                        using (FileStream fileStream = new FileStream(directorypath + $"/User_Profiles/{dataforohs}.json", FileMode.Open, FileAccess.Read))
                        {
                            fileStream.Read(firstNineBytes, 0, 9);
                            fileStream.Close();
                        }

                        if (HTTPserver.httpkey != "" && await Task.Run(() => Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 })))
                        {
                            byte[] src = File.ReadAllBytes(directorypath + $"/User_Profiles/{dataforohs}.json");
                            byte[] dst = new byte[src.Length - 9];

                            Array.Copy(src, 9, dst, 0, dst.Length);

                            tempreader = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                                        CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
                        }
                        else
                        {
                            tempreader = File.ReadAllText(directorypath + $"/User_Profiles/{dataforohs}.json");
                        }

                        // Deserialize the JSON string into a dynamic object
                        dynamic jsonObject = JsonConvert.DeserializeObject<dynamic>(tempreader);

                        // Get the value using the name of the key as a string
                        dataforohs = (string)inputjsonObject["key"];

                        if (jsonObject.Key[dataforohs] != null)
                        {
                            value = jsonObject.Key[dataforohs];
                        }
                    }
                }
                else
                {
                    if (File.Exists(directorypath + $"/Global.json"))
                    {
                        string tempreader = "";

                        byte[] firstNineBytes = new byte[9];

                        using (FileStream fileStream = new FileStream(directorypath + $"/Global.json", FileMode.Open, FileAccess.Read))
                        {
                            fileStream.Read(firstNineBytes, 0, 9);
                            fileStream.Close();
                        }

                        if (HTTPserver.httpkey != "" && await Task.Run(() => Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 })))
                        {
                            byte[] src = File.ReadAllBytes(directorypath + $"/Global.json");
                            byte[] dst = new byte[src.Length - 9];

                            Array.Copy(src, 9, dst, 0, dst.Length);

                            tempreader = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                                        CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
                        }
                        else
                        {
                            tempreader = File.ReadAllText(directorypath + $"/Global.json");
                        }

                        // Deserialize the JSON string into a dynamic object
                        dynamic jsonObject = JsonConvert.DeserializeObject<dynamic>(tempreader);

                        // Get the value using the name of the key as a string
                        dataforohs = (string)inputjsonObject["key"];

                        if (jsonObject.Key[dataforohs] != null)
                        {
                            value = jsonObject.Key[dataforohs];
                        }
                    }
                    else if ((string)inputjsonObject["key"] == "vickie_version")
                    {
                        value = 7; // Random value for vickie if file not exist.
                    }
                }

                if (batchparams != "")
                {
                    return value.ToString();
                }

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", "{ [\"status\"] = \"success\", [\"value\"] = " + value.ToString() + " }"));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    dataforohs = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a global/get/ or user/get/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the user/get/ request : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        private static async Task<string> user_id(HttpListenerContext context, string userAgent, string batchparams)
        {
            try
            {
                string dataforohs = "";

                if (batchparams == "")
                {
                    var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                    Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                    dataforohs = data.GetParameterValue("data");

                    // Execute the Lua script and get the result
                    object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                    if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                    {
                        dataforohs = returnValues[0]?.ToString();
                    }
                    else
                    {
                        Console.WriteLine($"OHS Server : {userAgent} Requested a userid/ method, but the lua errored out!");
                    }
                }
                else
                {
                    dataforohs = batchparams;
                }

                // Parsing the JSON string
                JObject jsonObject = JObject.Parse(dataforohs);

                // Getting the value of the "user" field
                dataforohs = (string)jsonObject["user"];

                if (batchparams != "")
                {
                    return UniqueNumberGenerator.GenerateUniqueNumber(dataforohs).ToString();
                }

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", "{ [\"status\"] = \"success\", [\"value\"] = " + UniqueNumberGenerator.GenerateUniqueNumber(dataforohs).ToString() + " }"));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    dataforohs = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a userid/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the userid/ request : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        private static async Task<string> user_getwritekey(HttpListenerContext context, string userAgent, string batchparams)
        {
            try
            {
                string dataforohs = "";

                if (batchparams == "")
                {
                    var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                    Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                    dataforohs = data.GetParameterValue("data");

                    // Execute the Lua script and get the result
                    object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                    if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                    {
                        dataforohs = returnValues[0]?.ToString();
                    }
                    else
                    {
                        Console.WriteLine($"OHS Server : {userAgent} Requested a user/getwritekey/ method, but the lua errored out!");
                    }
                }
                else
                {
                    dataforohs = batchparams;
                }

                // Parsing the JSON string
                JObject jsonObject = JObject.Parse(dataforohs);

                dataforohs = Misc.GetFirstEightCharacters(Misc.CalculateMD5Hash((string)jsonObject["user"]));

                if (batchparams != "")
                {
                    return "{ [\"writeKey\"] = \"" + dataforohs + "\" }";
                }

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", "{ [\"status\"] = \"success\",[\"value\"] = { [\"writeKey\"] = \"" + dataforohs + "\" } }"));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    dataforohs = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a user/getwritekey/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the user/getwritekey/ request : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        private static async Task<string> leaderboard_requestbyusers(HttpListenerContext context, string userAgent, string directoryPath, string batchparams)
        {
            try
            {
                string dataforohs = "";

                if (batchparams == "")
                {
                    var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                    Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                    dataforohs = data.GetParameterValue("data");

                    // Execute the Lua script and get the result
                    object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                    if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                    {
                        dataforohs = requestbyusers(returnValues[0]?.ToString(), directoryPath);
                    }
                    else
                    {
                        Console.WriteLine($"OHS Server : {userAgent} Requested a leaderboard/requestbyusers/ method, but the lua errored out!");
                    }
                }
                else
                {
                    dataforohs = requestbyusers(batchparams, directoryPath);
                }

                if (dataforohs == "")
                {
                    dataforohs = "{}";
                }

                if (batchparams != "")
                {
                    return "{ [\"entries\"] = " + dataforohs + " }";
                }

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", "{ [\"status\"] = \"success\",[\"value\"] = { [\"entries\"] = " + dataforohs + " } }"));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    dataforohs = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a leaderboard/requestbyusers/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");

                        context.Response.Close();
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the leaderboard/requestbyusers/ request and creating the file/http response : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        private static async Task<string> leaderboard_requestbyrank(HttpListenerContext context, string userAgent, string directoryPath, string batchparams)
        {
            try
            {
                string dataforohs = "";

                if (batchparams == "")
                {
                    var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                    Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                    dataforohs = data.GetParameterValue("data");

                    // Execute the Lua script and get the result
                    object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                    if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                    {
                        dataforohs = requestbyrank(returnValues[0]?.ToString(), directoryPath);
                    }
                    else
                    {
                        Console.WriteLine($"OHS Server : {userAgent} Requested a leaderboard/requestbyrank/ method, but the lua errored out!");
                    }
                }
                else
                {
                    dataforohs = requestbyrank(batchparams, directoryPath);
                }

                if (dataforohs == "")
                {
                    dataforohs = "{}";
                }

                if (batchparams != "")
                {
                    return "{ [\"entries\"] = " + dataforohs + " }";
                }

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", "{ [\"status\"] = \"success\",[\"value\"] = { [\"entries\"] = " + dataforohs + "} }"));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    dataforohs = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a leaderboard/requestbyrank/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{dataforohs}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");

                        context.Response.Close();
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the leaderboard/requestbyrank/ request and creating the file/http response : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        private static async Task<string> leaderboard_updatessameentry(HttpListenerContext context, string userAgent, string directoryPath, string batchparams)
        {
            try
            {
                string resultfromjamin = "";

                string writekey = "11111111";

                if (batchparams == "")
                {
                    var data = MultipartFormDataParser.Parse(context.Request.InputStream, Misc.ExtractBoundary(context.Request.ContentType));

                    Console.WriteLine($"OHS Server : {userAgent} issued a OHS request : Version - {data.GetParameterValue("version")}");

                    string hasheddataforohs = data.GetParameterValue("data");

                    string dataforohs = hasheddataforohs.Substring(8);

                    writekey = dataforohs.Substring(0, 8);

                    // Execute the Lua script and get the result
                    object[] returnValues = Misc.ExecuteLuaScript(jamindecrypt.Replace("PUT_ENCRYPTEDJAMINVALUE_HERE", dataforohs.Substring(8)));

                    if (!string.IsNullOrEmpty(returnValues[0]?.ToString()))
                    {
                        resultfromjamin = returnValues[0]?.ToString();
                    }
                    else
                    {
                        Console.WriteLine($"OHS Server : {userAgent} Requested a leaderboard/updatessameentry/ method, but the lua errored out!");
                    }
                }
                else
                {
                    resultfromjamin = batchparams;

                    // TODO! writekey must be somewhere.
                }

                if (writekey == "")
                {
                    Console.WriteLine($"OHS Server : {userAgent} Provided no WriteKey in leaderboard/updatessameentry/, we forbid!");

                    if (batchparams != "")
                    {
                        return "";
                    }
                    else
                    {
                        if (context.Response.OutputStream.CanWrite)
                        {
                            try
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                                context.Response.OutputStream.Close();
                            }
                            catch (Exception ex1)
                            {
                                Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Client Disconnected early");
                        }

                        context.Response.Close();

                        GC.Collect();

                        return "";
                    }
                }

                // Deserialize the JSON string
                ScoreBoardUpdate rootObject = JsonConvert.DeserializeObject<ScoreBoardUpdate>(resultfromjamin);

                // Extract the values
                string user = rootObject.user;
                int score = rootObject.score;
                string[] keys = rootObject.keys;

                StringBuilder resultBuilder = new StringBuilder();

                foreach (var key in keys)
                {
                    string scoreboardfile = directoryPath + $"/scoreboard_{key}.json";

                    if (File.Exists(scoreboardfile))
                    {
                        string tempreader = "";

                        byte[] firstNineBytes = new byte[9];

                        using (FileStream fileStream = new FileStream(scoreboardfile, FileMode.Open, FileAccess.Read))
                        {
                            fileStream.Read(firstNineBytes, 0, 9);
                            fileStream.Close();
                        }

                        if (HTTPserver.httpkey != "" && await Task.Run(() => Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 })))
                        {
                            byte[] src = File.ReadAllBytes(scoreboardfile);
                            byte[] dst = new byte[src.Length - 9];

                            Array.Copy(src, 9, dst, 0, dst.Length);

                            tempreader = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                                        CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
                        }
                        else
                        {
                            tempreader = File.ReadAllText(scoreboardfile);
                        }

                        string updatedScoreboard = UpdateScoreboard(tempreader, user, score, scoreboardfile);

                        if (resultBuilder.Length == 0)
                        {
                            resultBuilder.Append($"[\"{key}\"] = {updatedScoreboard}");
                        }
                        else
                        {
                            resultBuilder.Append($", [\"{key}\"] = {updatedScoreboard}");
                        }
                    }
                }

                resultfromjamin = resultBuilder.ToString();

                if (batchparams != "")
                {
                    if (resultfromjamin == "")
                    {
                        return "";
                    }

                    return "{ [\"writeKey\"] = \"" + writekey + "\", " + resultfromjamin + " }";
                }

                string returnvalue = "";

                if (resultfromjamin == "")
                {
                    returnvalue = "{ [\"status\"] = \"failed\" }";
                }
                else
                {
                    returnvalue = "{ [\"status\"] = \"success\",[\"value\"] = {[\"writeKey\"] = \"" + writekey + "\", " + resultfromjamin + "} }";
                }

                // Execute the Lua script and get the result
                object[] returnValues2nd = Misc.ExecuteLuaScript(jaminencrypt.Replace("PUT_TABLEINPUT_HERE", returnvalue));

                if (!string.IsNullOrEmpty(returnValues2nd[0]?.ToString()))
                {
                    resultfromjamin = returnValues2nd[0]?.ToString();
                }
                else
                {
                    Console.WriteLine($"OHS Server : {userAgent} Requested a leaderboard/updatessameentry/ method, but the lua errored out!");
                }

                byte[] postresponsetooutput = Encoding.UTF8.GetBytes($"<ohs>{resultfromjamin}</ohs>");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.Headers.Set("Content-Type", "application/xml;charset=UTF-8");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentLength64 = postresponsetooutput.Length;
                        context.Response.OutputStream.Write(postresponsetooutput, 0, postresponsetooutput.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OHS Server : thrown an exception in ProcessRequest while processing the leaderboard/updatessameentry/ request : {ex}");

                // Return an internal server error response
                byte[] InternnalError = Encoding.UTF8.GetBytes("An Error as occured, please retry.");

                if (context.Response.OutputStream.CanWrite)
                {
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentLength64 = InternnalError.Length;
                        context.Response.OutputStream.Write(InternnalError, 0, InternnalError.Length);
                        context.Response.OutputStream.Close();
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"Client Disconnected early and thrown an exception {ex1}");
                    }
                }
                else
                {
                    Console.WriteLine("Client Disconnected early");
                }
            }

            context.Response.Close();

            GC.Collect();

            return "";
        }

        public static string UpdateScoreboard(string json, string nameToUpdate, int newScore, string scoreboardfile)
        {
            bool noedits = false;

            string scoreboarddata = "";

            // Step 1: Deserialize JSON string into a C# object
            Scoreboard scoreboard = JsonConvert.DeserializeObject<Scoreboard>(json);

            // Step 2: Find the entry to update or the appropriate position for the new entry
            ScoreboardEntry entryToUpdate = null;

            int newIndex = -1;

            for (int i = 0; i < scoreboard.Entries.Count; i++)
            {
                var entry = scoreboard.Entries[i];

                if (newScore >= entry.Score)
                {
                    newIndex = i;

                    break;
                }
            }

            // Step 3: Add the new entry at the appropriate position
            if (newIndex >= 0)
            {
                scoreboard.Entries.Insert(newIndex, new ScoreboardEntry
                {
                    Name = nameToUpdate,
                    Score = newScore
                });

                // Step 4: Calculate the number of entries to maintain based on existing entries
                int maxEntries = scoreboard.Entries.Count;

                // Step 5: Remove any excess entries if the scoreboard exceeds the calculated number of entries
                while (scoreboard.Entries.Count >= maxEntries)
                {
                    scoreboard.Entries.RemoveAt(scoreboard.Entries.Count - 1);
                }
            }
            else
            {
                Console.WriteLine($"OHS ScoreBoard: Cannot add entry with name '{nameToUpdate}' and score '{newScore}'.");

                noedits = true;
            }

            if (!noedits)
            {
                // Step 6: Sort the entries based on the new scores
                scoreboard.Entries.Sort((a, b) => b.Score.CompareTo(a.Score));

                // Step 7: Adjust the ranks accordingly
                for (int i = 0; i < scoreboard.Entries.Count; i++)
                {
                    scoreboard.Entries[i].Rank = i + 1;
                }

                // Step 5: Serialize the updated object back to a JSON string
                string updatedscoreboard = JsonConvert.SerializeObject(scoreboard, Formatting.Indented);

                using (FileStream fs = new FileStream(scoreboardfile, FileMode.Create))
                {
                    // Serialize the updated JSON back to a byte array
                    byte[] updatedJsonString = Encoding.UTF8.GetBytes(updatedscoreboard);

                    if (HTTPserver.httpkey != "")
                    {
                        byte[] outfile = new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6C, 0x65, 0x64, 0x65, 0x73 };

                        byte[] encryptedbuffer = Misc.Combinebytearay(outfile, CRYPTOSPORIDIUM.TRIPLEDES.EncryptData(CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey), updatedJsonString));

                        fs.Write(encryptedbuffer, 0, encryptedbuffer.Length);
                        fs.Flush();
                        fs.Dispose();
                    }
                    else
                    {
                        fs.Write(updatedJsonString, 0, updatedJsonString.Length);
                        fs.Flush();
                        fs.Dispose();
                    }

                    Console.WriteLine($"File {scoreboardfile} has been uploaded to HTTP");
                }
            }

            if (noedits)
            {
                // Little optimization, can we avoid reading the file if it's not necessary.
                // We can loose "alittlebit" of precision, but hey, we are not one millisecond close.

                scoreboarddata = json;
            }
            else
            {
                byte[] firstNineBytes = new byte[9];

                using (FileStream fileStream = new FileStream(scoreboardfile, FileMode.Open, FileAccess.Read))
                {
                    fileStream.Read(firstNineBytes, 0, 9);
                    fileStream.Close();
                }

                if (HTTPserver.httpkey != "" && Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 }))
                {
                    byte[] src = File.ReadAllBytes(scoreboardfile);
                    byte[] dst = new byte[src.Length - 9];

                    Array.Copy(src, 9, dst, 0, dst.Length);

                    scoreboarddata = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                                CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
                }
                else
                {
                    scoreboarddata = File.ReadAllText(scoreboardfile);
                }
            }

            // Step 1: Parse JSON to C# objects
            var jsonData = JsonConvert.DeserializeObject<JObject>(scoreboarddata);

            var entries = jsonData["Entries"].ToObject<List<ScoreboardEntry>>();

            // Step 2: Convert to Lua table structure
            var luaTable = new Dictionary<int, Dictionary<string, object>>();

            foreach (var entry in entries)
            {
                var rankData = new Dictionary<string, object>
                {
                    { "[\"user\"]", $"\"{entry.Name}\"" }, // Enclose string in double quotes and put it inside the brackets
                    { "[\"score\"]", entry.Score } // For numbers, no need to enclose in quotes and put it inside the brackets
                };

                luaTable.Add(entry.Rank, rankData);
            }

            // Step 3: Format the Lua table as a string using regex
            var luaString = FormatScoreBoardLuaTable(luaTable);

            return luaString;
        }

        public static string requestbyusers(string jsontable, string scoreboardpath)
        {
            string scoreboardfile = "";

            string returnvalue = "";

            try
            {
                if (!Directory.Exists(scoreboardpath))
                {
                    return "";
                }
                else
                {
                    ScoreBoardUsersRequest data = JsonConvert.DeserializeObject<ScoreBoardUsersRequest>(jsontable);

                    if (data != null && data.Users != null)
                    {
                        scoreboardfile = scoreboardpath + $"/scoreboard_{data.Key}.json";

                        if (!File.Exists(scoreboardfile))
                        {
                            return "";
                        }

                        StringBuilder resultBuilder = new StringBuilder();

                        foreach (string user in data.Users)
                        {
                            string tempreader = "";

                            byte[] firstNineBytes = new byte[9];

                            using (FileStream fileStream = new FileStream(scoreboardfile, FileMode.Open, FileAccess.Read))
                            {
                                fileStream.Read(firstNineBytes, 0, 9);
                                fileStream.Close();
                            }

                            if (HTTPserver.httpkey != "" && Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 }))
                            {
                                byte[] src = File.ReadAllBytes(scoreboardfile);
                                byte[] dst = new byte[src.Length - 9];

                                Array.Copy(src, 9, dst, 0, dst.Length);

                                tempreader = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                                            CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
                            }
                            else
                            {
                                tempreader = File.ReadAllText(scoreboardfile);
                            }

                            var jsonData = JsonConvert.DeserializeObject<JObject>(tempreader);

                            var entries = jsonData["Entries"].ToObject<List<ScoreboardEntry>>();

                            foreach (var entry in entries)
                            {
                                if (entry.Name == user)
                                {
                                    if (entry.Score != null)
                                    {
                                        if (resultBuilder.Length == 0)
                                        {
                                            resultBuilder.Append($"{{ [" + entry.Rank + $"] = {{ [\"user\"] = \"{entry.Name}\", [\"score\"] = {entry.Score.ToString()} }}");
                                        }
                                        else
                                        {
                                            resultBuilder.Append($", [" + entry.Rank + $"] = {{ [\"user\"] = \"{entry.Name}\", [\"score\"] = {entry.Score.ToString()} }}");
                                        }
                                    }
                                    else
                                    {
                                        if (resultBuilder.Length == 0)
                                        {
                                            resultBuilder.Append($"{{ [" + entry.Rank + $"] = {{ [\"user\"] = \"{entry.Name}\", [\"score\"] = {0.ToString()} }}");
                                        }
                                        else
                                        {
                                            resultBuilder.Append($", [" + entry.Rank + $"] = {{ [\"user\"] = \"{entry.Name}\", [\"score\"] = {0.ToString()} }}");
                                        }
                                    }
                                }
                            }
                        }

                        if (resultBuilder.Length != 0)
                        {
                            resultBuilder.Append(" }");
                        }

                        returnvalue = resultBuilder.ToString();
                    }
                    else
                    {
                        Console.WriteLine("OHS Server : requestbyusers - Invalid JSON format or missing 'users' field.");

                        return "";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing JSON: {ex}");

                return "";
            }

            return returnvalue;
        }

        public static string requestbyrank(string jsontable, string scoreboardpath)
        {
            // Sometimes requestbyrank was used to create the scoreboard.

            int numEntries = 0;

            int start = 1;

            string user = "";

            string key = "";

            JObject jsonDatainit = GetJsonData(jsontable);

            if (jsonDatainit != null)
            {
                numEntries = (int)jsonDatainit["numEntries"];
                start = (int)jsonDatainit["start"];
                user = (string)jsonDatainit["user"];
                key = (string)jsonDatainit["key"];
            }
            else
            {
                Console.WriteLine("OHS Server : requestbyrank - Invalid JSON format.");

                return "";
            }

            if (user == null || user == "")
            {
                Console.WriteLine("OHS Server : requestbyrank - No username so not allowed!");

                return "";
            }

            if (!Directory.Exists(scoreboardpath))
            {
                Directory.CreateDirectory(scoreboardpath);
            }

            string scoreboardfile = scoreboardpath + $"/scoreboard_{key}.json";

            if (!File.Exists(scoreboardfile))
            {
                Scoreboard scoreboard = GenerateSampleScoreboard(numEntries);

                string json = JsonConvert.SerializeObject(scoreboard, Formatting.Indented);

                using (FileStream fs = new FileStream(scoreboardfile, FileMode.Create))
                {
                    // Serialize the updated JSON back to a byte array
                    byte[] updatedJsonString = Encoding.UTF8.GetBytes(json);

                    if (HTTPserver.httpkey != "")
                    {
                        byte[] outfile = new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6C, 0x65, 0x64, 0x65, 0x73 };

                        byte[] encryptedbuffer = Misc.Combinebytearay(outfile, CRYPTOSPORIDIUM.TRIPLEDES.EncryptData(CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey), updatedJsonString));

                        fs.Write(encryptedbuffer, 0, encryptedbuffer.Length);
                        fs.Flush();
                        fs.Dispose();
                    }
                    else
                    {
                        fs.Write(updatedJsonString, 0, updatedJsonString.Length);
                        fs.Flush();
                        fs.Dispose();
                    }

                    Console.WriteLine($"File {scoreboardfile} has been uploaded to HTTP");
                }
            }

            byte[] firstNineBytes = new byte[9];

            using (FileStream fileStream = new FileStream(scoreboardfile, FileMode.Open, FileAccess.Read))
            {
                fileStream.Read(firstNineBytes, 0, 9);
                fileStream.Close();
            }

            if (HTTPserver.httpkey != "" && Misc.FindbyteSequence(firstNineBytes, new byte[] { 0x74, 0x72, 0x69, 0x70, 0x6c, 0x65, 0x64, 0x65, 0x73 }))
            {
                byte[] src = File.ReadAllBytes(scoreboardfile);
                byte[] dst = new byte[src.Length - 9];

                Array.Copy(src, 9, dst, 0, dst.Length);

                scoreboardfile = Encoding.UTF8.GetString(CRYPTOSPORIDIUM.TRIPLEDES.DecryptData(dst,
                            CRYPTOSPORIDIUM.TRIPLEDES.GetEncryptionKey(HTTPserver.httpkey)));
            }
            else
            {
                scoreboardfile = File.ReadAllText(scoreboardfile);
            }

            // Step 1: Parse JSON to C# objects
            var jsonData = JsonConvert.DeserializeObject<JObject>(scoreboardfile);

            var entries = jsonData["Entries"].ToObject<List<ScoreboardEntry>>();

            // Step 2: Convert to Lua table structure
            var luaTable = new Dictionary<int, Dictionary<string, object>>();

            int i = 1;

            foreach (var entry in entries)
            {
                if (i >= start)
                {
                    var rankData = new Dictionary<string, object>
                    {
                        { "[\"user\"]", $"\"{entry.Name}\"" }, // Enclose string in double quotes and put it inside the brackets
                        { "[\"score\"]", entry.Score } // For numbers, no need to enclose in quotes and put it inside the brackets
                    };

                    luaTable.Add(entry.Rank, rankData);
                }
            }

            // Step 3: Format the Lua table as a string using regex
            var luaString = FormatScoreBoardLuaTable(luaTable);

            return luaString;
        }

        // Helper method to format the Lua table as a string
        private static string FormatScoreBoardLuaTable(Dictionary<int, Dictionary<string, object>> luaTable)
        {
            var luaString = "{\n";
            foreach (var rankData in luaTable)
            {
                luaString += $"    [{rankData.Key}] = {{\n";
                foreach (var kvp in rankData.Value)
                {
                    luaString += $"        {kvp.Key} = {kvp.Value},\n"; // We already formatted the keys and values accordingly
                }
                luaString = RemoveTrailingComma(luaString); // Remove the trailing comma for the last element in each number category
                luaString += "    },\n";
            }
            luaString += "}";

            // Remove trailing commas
            luaString = RemoveTrailingComma(luaString);

            return luaString;
        }

        // Helper method to remove the trailing comma from the Lua table string
        private static string RemoveTrailingComma(string input)
        {
            return Regex.Replace(input, @",(\s*})|(\s*]\s*})", "$1$2");
        }

        public static JObject GetJsonData(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while parsing JSON: {ex.Message}");
                return null;
            }
        }
        public static Scoreboard GenerateSampleScoreboard(int numEntries)
        {
            Scoreboard scoreboard = new Scoreboard();
            scoreboard.Entries = new List<ScoreboardEntry>();

            Random random = new Random();
            for (int i = 1; i <= numEntries; i++)
            {
                string playerName = ScoreboardNameGenerator.GenerateRandomName();
                int score = random.Next(100, 1000); // Generate a random score between 100 and 999
                scoreboard.Entries.Add(new ScoreboardEntry { Name = playerName, Score = score });
            }

            // Sort the entries by score in descending order
            scoreboard.Entries.Sort((entry1, entry2) => entry2.Score.CompareTo(entry1.Score));

            // Assign ranks based on the sorted order
            for (int i = 0; i < scoreboard.Entries.Count; i++)
            {
                scoreboard.Entries[i].Rank = i + 1;
            }

            return scoreboard;
        }
    }
    public class ScoreboardEntry
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public int Rank { get; set; } // Add this property to hold the rank
    }

    public class Scoreboard
    {
        public List<ScoreboardEntry> Entries { get; set; }
    }

    public class ScoreBoardUpdate
    {
        public string user { get; set; }
        public string[] keys { get; set; }
        public int score { get; set; }
        public object[] value { get; set; }
    }

    public class ScoreBoardUsersRequest
    {
        public string[] Users { get; set; }
        public string Key { get; set; }
    }

    public class BatchCommand
    {
        public string Method { get; set; }
        public JObject Data { get; set; }
        public string Project { get; set; }
    }
    public class OHSUserProfile
    {
        public string User { get; set; }
        public object Key { get; set; }
    }

    public class OHSGlobalProfile
    {
        public object Key { get; set; }
    }
}