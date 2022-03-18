using NewsDistribution;

Subscriber andrey = new("andrey@yandex.ru");
Subscriber egor = new("egor@gmail.com");
Subscriber pavel = new("pavel@t.me");

Mail mail = new();
mail.RegisterTarget(andrey);
mail.RegisterTarget(egor);

mail.Update(new News("Peace to the world.",
    "Slavs are brothers.",
    "Peace, friendship, gum."));

mail.RegisterTarget(pavel);

mail.Update(new News("Mariupol is burning.",
    "Fear.",
    "Horror."));

mail.RemoveTarget(pavel);

mail.Update(new News("The [censored word in Russia] must be over.",
    "The end does not justify the means.",
    "Good conquers evil."));
