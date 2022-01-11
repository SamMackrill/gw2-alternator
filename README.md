gw2-alternator

Tool to help logging-in to multiple Guild Wars 2 alt accounts so as to harvest the daily rewards.

Features:
 * Import account details from GW2Launcher and/or GW2Launchbuddy
 * Automatically login to accounts with no user input
 * Assist with collection of rewards every few days
 * Application has no access to your GW2 authentication details

Getting Started
 * On application launch you will be asked to confirm admin access
  * This is so symbolic links can be created (similar to GW2LaunchBuddy)
 * If you have no accounts defined then go to settings and import from GW2Launcher and/or GW2Launchbuddy
 * Hit Login to login to all the accounts
  * Only accounts not logged-in that day will launch (unless you clock Force all or make a selection)
  * If you have > 20 accounts expect that some fail (they will retry automatically)
  * The first character on the account will be automatically selected, this will be the one that logged in most recently
  * Only accounts that have not logged-in since reset will launch
 * You can select accounts if you just want to launch a subset, this will ignore the filtering
 * To harvest it is best not to run the Login step that day as this will trigger the login throttling

 Working with Multiple Accounts
 * The anti-botting measures tend to make dealing with more than 10 accounts difficult
 * use email aliases to set-up your accounts : https://support.google.com/a/users/answer/9308648?hl=en
 * Logging into many (>10? ) accounts on https://www.guildwars2.com/ may cause a lockout that takes 24 hours to clear
  * /!\ Someting went wrong. Please try again in a few minutes
  * Speculation: This is linked to using email aliases
  * Using a VPN seems not to help here
 * Logging into the game will becoume slower after multiple (>10?) accounts and may block entirely
  * Using a VPN will help
  * Waiting a few minutes (5?) will help
  * GW2-Alternator tries to counter this by slowing down the login attempts
   * However the delay required depend on the past login history and is difficult to guess
   * There are tuning parameters in Settings to help adjust this

Advanced
 * Settings, Account details and logs are found here: %AppData%\gw2-alternator
 * Given a GW2 API key then Laurels and Mystic Coins will be counted
  * Account/

If anybody wants to help I would be delighted!

TO DO
 * Updates when the game version changes 
  * You can use GW2Launcher or GW2Launchbuddy to do this
 * VPN switching
 * More robust login delay
 * Better GW2 State detection
  * e.g. when error occurs or when login is very slow
 * Fix Automation of releases
 * Decouple Client from Account
 * Improve Unit Testing
 * Improve error reporting
 * Investigate using GW2Launcher multiple Windows User accounts approach (this avoids Admin requirement)
 * Add way to set API key
 * Improved numeric settings input and validation


Credits:
* GW2 API access using GW2Sharp https://github.com/Archomeda/Gw2Sharp
* MVVM async using https://github.com/brminnick/AsyncAwaitBestPractices
* Logging using NLog https://nlog-project.org/
* Unit Testing using xUnit https://xunit.net/ and FluentAssertions https://fluentassertions.com/
* Main icon by https://www.flaticon.com/authors/ingmixa
* Other icons from: https://www.iconsdb.com/ with color: #FB651D
