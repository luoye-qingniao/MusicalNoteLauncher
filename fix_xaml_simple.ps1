$simpleXaml = @"
<UserControl x:Class="MusicalNoteLauncher.Pages.MultiplayerSocialPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="#1E1E1E">
    <Grid Background="#1E1E1E">
        <Grid x:Name="MainPage" Visibility="Visible">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                <TextBlock Text="Terracotta | 陶瓦联机" Foreground="#D2691E" FontSize="32" FontWeight="Bold" HorizontalAlignment="Center" Margin="0,0,0,20"/>
                <StackPanel Orientation="Horizontal" Margin="0,20,0,0">
                    <Button x:Name="btnHost" Width="200" Height="150" Background="#2D2D2D" BorderBrush="#4CAF50" BorderThickness="2" Click="BtnHost_Click">
                        <StackPanel>
                            <TextBlock Text="HOST" FontSize="24" FontWeight="Bold" Foreground="#4CAF50" HorizontalAlignment="Center"/>
                            <TextBlock Text="我想当房主" Foreground="White" FontSize="14" HorizontalAlignment="Center" Margin="0,5,0,0"/>
                        </StackPanel>
                    </Button>
                    <Border Width="30"/>
                    <Button x:Name="btnClient" Width="200" Height="150" Background="#2D2D2D" BorderBrush="#2196F3" BorderThickness="2" Click="BtnClient_Click">
                        <StackPanel>
                            <TextBlock Text="JOIN" FontSize="24" FontWeight="Bold" Foreground="#2196F3" HorizontalAlignment="Center"/>
                            <TextBlock Text="我想当房客" Foreground="White" FontSize="14" HorizontalAlignment="Center" Margin="0,5,0,0"/>
                        </StackPanel>
                    </Button>
                </StackPanel>
            </StackPanel>
        </Grid>
        <Grid x:Name="HostPage" Visibility="Collapsed">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                <TextBlock Text="请进入单人存档，按ESC选择对局域网开放" Foreground="#D2691E" FontSize="14" HorizontalAlignment="Center" Margin="0,0,0,20"/>
                <StackPanel Orientation="Horizontal">
                    <Button x:Name="btnCreateRoom" Content="创建局域网世界" Background="#4CAF50" Foreground="White" Padding="20,10" Click="BtnCreateRoom_Click"/>
                    <Border Width="20"/>
                    <Button x:Name="btnBackFromHost" Content="返回" Background="#383838" Foreground="White" Padding="20,10" Click="BtnBack_Click"/>
                </StackPanel>
            </StackPanel>
        </Grid>
        <Grid x:Name="ClientPage" Visibility="Collapsed">
            <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Width="400">
                <TextBlock Text="请输入邀请码" Foreground="#D2691E" FontSize="18" HorizontalAlignment="Center" Margin="0,0,0,20"/>
                <TextBox x:Name="txtInviteCode" Background="#2D2D2D" Foreground="White" Width="300" Height="40" Margin="0,0,0,20"/>
                <StackPanel Orientation="Horizontal">
                    <Button x:Name="btnJoinRoom" Content="加入房间" Background="#2196F3" Foreground="White" Padding="25,10" Click="BtnJoinRoom_Click"/>
                    <Border Width="20"/>
                    <Button x:Name="btnBackFromClient" Content="返回" Background="#383838" Foreground="White" Padding="25,10" Click="BtnBack_Click"/>
                </StackPanel>
            </StackPanel>
        </Grid>
    </Grid>
</UserControl>
"@
[System.IO.File]::WriteAllText("Pages\MultiplayerSocialPage.xaml", $simpleXaml, [System.Text.Encoding]::UTF8)
